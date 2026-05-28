# **TP 3 \- Partie C : Logique Métier & Agrégation (Préparation Front)**

**Module :** M-4EADL-301 \- Développement Avancé & Extreme Programming

**Séance :** 3 / 8

**Focus :** Relations 1-N, Eager Loading (.Include), DTOs complexes, Logique Métier.

## **Objectif de la Partie C**

Jusqu'à présent, nous avons fait du CRUD simple. C'est techniquement fonctionnel, mais pauvre fonctionnellement.  
Pour que l'application LevelUp ait du sens, nous devons implémenter deux concepts clés avant d'attaquer le Front-End :

1. **L'Historique d'Activité :** On ne donne pas de l'XP "magiquement". L'XP est la conséquence d'une **Activité** (ex: "A terminé le module API").  
2. **Le Profil Utilisateur :** Un endpoint unique qui agrège tout (Infos User \+ Badges \+ Dernières Activités) pour affichage.

## **Étape 1 : Enregistrer une Activité (Relation 1-N)**

Au lieu d'utiliser la route "triche" POST /users/{id}/xp créée précédemment, nous allons créer une vraie route métier.

### **1\. Le DTO (Request)**

Dans Dtos/ActivityDtos.cs (à créer) :

```csharp
namespace LevelUp.Api.Dtos;

public record CreateActivityRequest(string Description, int XpEarned);
```

### **2\. Le Repository (Core & Infra)**

Nous devons pouvoir ajouter une activité liée à un utilisateur.

* Interface (IUserRepository) :  
  Ajoutez : 
  ```csharp
  Task AddActivityAsync(int userId, Activity activity);
  ```
* **Implémentation (SqlUserRepository) :**  
```csharp
  public async Task AddActivityAsync(int userId, Activity activity)  
  {  
      // On lie l'activité à l'utilisateur  
      activity.UserId \= userId;  
      activity.Date \= DateTime.UtcNow;

      // Ajout via le DbContext  
      await \_context.Activities.AddAsync(activity);

      // METIER : Mettre à jour le TotalXP de l'utilisateur automatiquement  
      var user \= await \_context.Users.FindAsync(userId);  
      if (user \!= null)  
      {  
          user.TotalXP \+= activity.XPEarned;  
      }  
      // Note : Le SaveChanges sera appelé par le contrôleur  
  }
```

### **3\. L'Endpoint (API)**

Dans UserEndpoints.cs, remplacez ou ajoutez la route POST /users/{userId}/activities :

```csharp
group.MapPost("/{userId}/activities", async (int userId, CreateActivityRequest request, IUserRepository repo) \=\>  
{  
    var user \= await repo.GetByIdAsync(userId);  
    if (user is null) return Results.NotFound();

    var activity \= new Activity   
    {   
        Description \= request.Description,   
        XPEarned \= request.XpEarned   
    };

    await repo.AddActivityAsync(userId, activity);  
    await repo.SaveChangesAsync();

    return Results.Created($"/users/{userId}/activities", activity);  
});
```

## **Étape 2 : Le Profil Complet (Agrégation & Include)**

Le Front-End aura besoin d'afficher une page "Profil" avec : le nom, le niveau, la liste des badges et les 5 dernières activités. Faire 3 appels API séparés est une mauvaise pratique (latence). Nous allons faire un endpoint agrégé.

### **1\. Le DTO Completu (Response)**

Dans Dtos/UserDtos.cs, ajoutez :

```csharp
// Un DTO riche qui contient des listes imbriquées  
public record UserProfileResponse(  
    int Id,   
    string Name,   
    string Email,   
    int TotalXP,   
    int Level, // Calculé  
    List\<string\> Badges, // Juste les noms ou URLs  
    List\<ActivityResponse\> RecentActivities  
);

public record ActivityResponse(string Description, int Xp, DateTime Date);
```

### **2\. Le Repository : Eager Loading**

C'est le point technique crucial. Par défaut, EF Core ne charge PAS les listes (Badges, Activities). Il faut utiliser .Include().

* **Interface :** 
```csharp
Task\<User?\> GetUserProfileAsync(int id);
```

* **Implémentation :**
```csharp
  public async Task\<User?\> GetUserProfileAsync(int id)  
  {  
      return await \_context.Users  
          .Include(u \=\> u.Badges)      // Charger les badges  
          .Include(u \=\> u.Activities)  // Charger les activités  
          .FirstOrDefaultAsync(u \=\> u.Id \== id);  
  }
```

### **3\. L'Endpoint "Profil"**

Ajoutez GET /users/{id}/profile dans UserEndpoints.cs.
```csharp
group.MapGet("/{id}/profile", async (int id, IUserRepository repo) \=\>  
{  
    var user \= await repo.GetUserProfileAsync(id);  
    if (user is null) return Results.NotFound();

    // Logique Métier : Calcul du niveau (ex: 1 niveau tous les 100 XP)  
    int level \= user.TotalXP / 100 \+ 1;

    // Mapping vers le DTO complexe  
    var response \= new UserProfileResponse(  
        user.Id,  
        user.Name,  
        user.Email,  
        user.TotalXP,  
        level,  
        user.Badges.Select(b \=\> b.Name).ToList(),  
        user.Activities  
            .OrderByDescending(a \=\> a.Date)  
            .Take(5) // On ne veut que les 5 dernières  
            .Select(a \=\> new ActivityResponse(a.Description, a.XPEarned, a.Date))  
            .ToList()  
    );

    return Results.Ok(response);  
});
```

## **Étape 3 : Challenge Métier (Autonomie)**

Votre application commence à avoir de la "gueule". Mais il manque un mécanisme de récompense automatique.

**Mission :** Implémenter une règle métier "Badge Automatique".

1. **La Règle :** Si un utilisateur dépasse **500 XP**, il doit recevoir automatiquement le badge "Vétéran" (s'il ne l'a pas déjà).  
2. **Où coder ça ?**  
   * Cette logique doit se trouver lors de l'ajout d'une activité (AddActivityAsync dans le Repo, ou mieux, dans une classe Service si on respectait SOLID à la lettre, mais restons dans le Repo pour l'instant).  
3. **Implémentation :**  
   * Dans AddActivityAsync, après avoir ajouté l'XP :  
   * Vérifiez si TotalXP \>= 500\.  
   * Vérifiez si la collection user.Badges contient déjà "Vétéran".  
   * Si non, créez le badge et ajoutez-le.  
4. **Test :**  
   * Utilisez le fichier .http.  
   * Créez un user.  
   * Ajoutez une activité qui rapporte 600 XP.  
   * Appelez le endpoint /profile et vérifiez que le badge "Vétéran" est apparu tout seul.

## **Étape 4 : Validation Finale (Fichier .http)**

Mettez à jour votre fichier LevelUp.http pour tester ce flux complet :

```http
### 1\. Créer un utilisateur  
POST {{host}}/users  
Content-Type: application/json  
{ "name": "Gamer One", "email": "gamer@test.com" }

### 2\. Lui ajouter une activité (Gain d'XP)  
POST {{host}}/users/1/activities  
Content-Type: application/json  
{ "description": "Victoire Boss Final", "xpEarned": 550 }

### 3\. Consulter son profil (Doit contenir XP, Activité ET Badge Vétéran auto)  
GET {{host}}/users/1/profile  
Accept: application/json
```

**Bravo \!** Vous avez maintenant une API qui a du sens.

* Elle gère des relations complexes.  
* Elle applique des règles métier.  
* Elle fournit des données formatées pour le futur Front-End.