# **TP 3 \- Partie B : Implémentation API, DTOs & Minimal APIs**

**Module :** M-4EADL-301 \- Développement Avancé & Extreme Programming

**Séance :** 3 / 8

**Focus :** Minimal APIs, Pattern DTO, Rigueur HTTP, Mapping.

## **Objectif de la Partie B**

Maintenant que l'architecture est découplée (Partie A), nous allons construire la "Porte d'Entrée" de notre application : l'API REST.

L'objectif est d'exposer nos données au monde extérieur de manière **sécurisée** (via DTOs) et **standardisée** (Codes HTTP corrects). Nous n'utiliserons pas de contrôleurs classiques mais les **Minimal APIs** (.NET 6+) pour plus de performance et de modernité.

## **Étape 1 : La Sécurité par le Design (DTOs)**

Nous ne devons **JAMAIS** exposer nos entités EF Core (User) directement.

* **Pourquoi ?**  
  * Sécurité : On ne veut pas exposer des champs techniques ou sensibles.  
  * Couplage : Si la BDD change, l'API ne doit pas changer (le contrat JSON doit rester stable).  
  * Overposting : Un utilisateur ne doit pas pouvoir modifier son propre solde d'XP.  
1. Dans le projet **LevelUp.Api**, créez un dossier **Dtos**.  
2. Créez un fichier UserDtos.cs. Nous allons utiliser des records pour leur immutabilité et leur syntaxe concise.

namespace LevelUp.Api.Dtos;

// \--- REQUESTS (Ce que le client envoie) \---

// Pour la création, on demande juste le strict minimum.  
// L'ID est généré par la BDD, l'XP commence à 0\.  
public record CreateUserRequest(string Name, string Email);

// Pour une action métier (Donner de l'XP)  
public record GiveXpRequest(int Amount);

// \--- RESPONSES (Ce que le client reçoit) \---

// On renvoie une vue complète mais sécurisée de l'utilisateur.  
// Note : On ne renvoie PAS la liste des Activités pour l'instant (Lazy Loading).  
public record UserResponse(int Id, string Name, string Email, int Xp);

## **Étape 2 : Organisation des Endpoints (Extension Methods)**

Pour ne pas surcharger le Program.cs, nous allons organiser nos routes dans des fichiers dédiés par domaine métier. C'est une bonne pratique essentielle pour ne pas finir avec un fichier de 2000 lignes.

1. Dans le projet **LevelUp.Api**, créez un dossier **Endpoints**.  
2. Créez une classe statique UserEndpoints.cs.  
3. Ajoutez une méthode d'extension MapUserEndpoints pour grouper les routes.

using LevelUp.Api.Dtos;  
using LevelUp.Core.Entities;  
using LevelUp.Core.Interfaces;  
using Microsoft.AspNetCore.Mvc;

namespace LevelUp.Api.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this WebApplication app)  
    {  
        // Tous les endpoints définis ici commenceront par "/users"  
        var group \= app.MapGroup("/users");

        // GET /users  
        group.MapGet("/", GetAllUsers);  
          
        // POST /users  
        group.MapPost("/", CreateUser);  
          
        // GET /users/{id}  
        group.MapGet("/{id}", GetUserById);

        // POST /users/{id}/xp (Action métier)  
        group.MapPost("/{id}/xp", GiveXp);  
    }

    // \--- HANDLERS (La logique des routes) \---

    // 1\. GET ALL  
    public static async Task\<IResult\> GetAllUsers(IUserRepository repo)  
    {  
        // Appel au Repository (Abstraction)  
        var users \= await repo.GetAllAsync();

        // Mapping Entity \-\> DTO (Manuel pour l'instant)  
        var response \= users.Select(u \=\> new UserResponse(u.Id, u.Name, u.Email, u.TotalXP));

        // Retour HTTP 200 OK  
        return Results.Ok(response);  
    }

    // 2\. GET BY ID  
    public static async Task\<IResult\> GetUserById(int id, IUserRepository repo)  
    {  
        var user \= await repo.GetByIdAsync(id);

        if (user is null)  
        {  
            // Retour HTTP 404 Not Found (Standard)  
            return Results.NotFound($"User with ID {id} not found.");  
        }

        return Results.Ok(new UserResponse(user.Id, user.Name, user.Email, user.TotalXP));  
    }

    // 3\. CREATE  
    public static async Task\<IResult\> CreateUser(  
        \[FromBody\] CreateUserRequest request,   
        IUserRepository repo)  
    {  
        // Validation basique (Fail Fast)  
        if (string.IsNullOrWhiteSpace(request.Name))  
            return Results.BadRequest("Le nom est obligatoire.");

        // Mapping DTO \-\> Entity  
        // On crée l'entité propre avec les valeurs par défaut métier (XP \= 0\)  
        var newUser \= new User  
        {  
            Name \= request.Name,  
            Email \= request.Email,  
            TotalXP \= 0   
        };

        // Appel au Repository  
        await repo.AddAsync(newUser);  
        await repo.SaveChangesAsync(); // C'est ici que l'ID est généré par SQL Server

        // Mapping Entity \-\> DTO (pour la réponse)  
        var responseDto \= new UserResponse(newUser.Id, newUser.Name, newUser.Email, newUser.TotalXP);

        // Retour HTTP 201 Created \+ Header Location  
        // Le client saura où trouver la ressource créée : /users/12  
        return Results.Created($"/users/{newUser.Id}", responseDto);  
    }

    // 4\. GIVE XP (Action Métier)  
    public static async Task\<IResult\> GiveXp(  
        int id,   
        \[FromBody\] GiveXpRequest request,   
        IUserRepository repo)  
    {  
        // 1\. Récupération  
        var user \= await repo.GetByIdAsync(id);  
        if (user is null) return Results.NotFound($"User {id} not found.");

        // 2\. Action Métier (Modification de l'état)  
        user.TotalXP \+= request.Amount;

        // 3\. Persistance  
        // Note : Pas besoin de "UpdateAsync", car l'objet est suivi par le DbContext (Change Tracking)  
        // tant qu'il provient du même Scope.  
        await repo.SaveChangesAsync();

        // 4\. Retour 204 No Content (L'action a réussi, pas de contenu à renvoyer)  
        // Ou 200 OK avec le nouvel état si le front en a besoin.  
        return Results.NoContent();  
    }  
}

## **Étape 3 : Activation des Routes**

Il faut maintenant dire à l'application de charger ces routes au démarrage.

1. Ouvrez Program.cs dans **LevelUp.Api**.  
2. Ajoutez l'espace de nom using LevelUp.Api.Endpoints;.  
3. Juste avant app.Run(), appelez votre méthode d'extension :

var app \= builder.Build();

// ... (Middlewares existants : Swagger, HttpsRedirection...)

// Enregistrement des endpoints  
app.MapUserEndpoints();

app.Run();

## **Étape 4 : Test via fichier .http (Developer Experience)**

Oubliez Postman ou Swagger UI pour le développement quotidien. Utilisez les fichiers .http intégrés : ils sont rapides, versionnables et exécutables directement dans l'IDE.

1. À la racine du projet API, créez un fichier nommé **LevelUp.http**.  
2. Collez le contenu suivant :

@host \= http://localhost:5000

\#\#\# 1\. Récupérer tous les utilisateurs (Liste vide au début)  
GET {{host}}/users  
Accept: application/json

\#\#\# 2\. Créer un nouvel utilisateur (Succès attendu : 201 Created)  
POST {{host}}/users  
Content-Type: application/json

{  
  "name": "Jean Codeur",  
  "email": "jean@levelup.com"  
}

\#\#\# 3\. Vérifier la création via l'ID (Remplacez l'ID par celui reçu)  
GET {{host}}/users/1  
Accept: application/json

\#\#\# 4\. Donner de l'XP (Action Métier)  
POST {{host}}/users/1/xp  
Content-Type: application/json

{  
  "amount": 50  
}

\#\#\# 5\. Vérifier que l'XP a augmenté  
GET {{host}}/users/1  
Accept: application/json

\#\#\# 6\. Test d'erreur : Créer un utilisateur invalide (Erreur attendue : 400 Bad Request)  
POST {{host}}/users  
Content-Type: application/json

{  
  "name": "",  
  "email": "badrequest@test.com"  
}

\#\#\# 7\. Test d'erreur : ID inexistant (Erreur attendue : 404 Not Found)  
GET {{host}}/users/9999  
Accept: application/json

3. Lancez l'API (F5 ou Ctrl+F5).  
4. Cliquez sur les petits boutons "Send Request" (flèche verte) dans le fichier .http pour tester vos routes.

## **Étape 5 : En autonomie (Attribution de Badges)**

Vous avez maintenant une API fonctionnelle pour les Users. À vous d'enrichir **LevelUp** avec une vraie logique métier : **Récompenser un utilisateur**.

**Contexte Métier :** Un administrateur doit pouvoir attribuer un badge existant à un utilisateur pour le féliciter (ex: "Expert C\#").

**Votre mission (à réaliser sans guide pas-à-pas) :**

### **A. Préparation des DTOs**

Créez les contrats suivants dans le dossier Dtos :

1. CreateBadgeRequest (Nom, ImageUrl, Description).  
2. BadgeResponse (Id, Nom, ImageUrl).  
3. AssignBadgeRequest (BadgeId). *Note : On ne passe que l'ID du badge, l'ID du user sera dans l'URL.*

### **B. Gestion des Badges (CRUD Simple)**

1. Créez BadgeEndpoints.cs.  
2. Implémentez POST /badges pour définir un nouveau type de badge dans le système (ex: "Bug Hunter").  
3. Implémentez GET /badges pour lister tous les badges disponibles.  
4. N'oubliez pas d'injecter IBadgeRepository (créé en Partie A) et d'enregistrer les routes dans Program.cs.

### **C. Le Coeur du Sujet : L'Attribution**

Implémentez la route POST /users/{userId}/badges qui permet de donner un badge à un utilisateur.

**Algorithme attendu :**

1. Récupérer l'ID de l'utilisateur (URL) et l'ID du badge (Body JSON).  
2. Vérifier que l'utilisateur existe via IUserRepository.  
   * *Si non :* Retourner 404 Not Found.  
3. Vérifier que le badge existe via IBadgeRepository.  
   * *Si non :* Retourner 404 Not Found.  
   * *Bonus :* Si l'utilisateur possède DÉJÀ ce badge, retourner 400 Bad Request ("Badge already assigned").  
4. Créer la relation (Ajouter le badge à la collection Badges de l'utilisateur).  
5. Sauvegarder (SaveChangesAsync).  
6. Retourner 201 Created ou 200 OK.

### **D. Validation Finale**

Ajoutez ce scénario dans votre fichier LevelUp.http :

1. Créer un User "Alice".
2. Créer un Badge "Architecte".
3. Attribuer le Badge "Architecte" à "Alice".
4. Vérifier via un GET /users/{id} (que vous devrez peut-être enrichir pour inclure les badges \!) que l'association est faite.

**Critère de réussite :** Le scénario complet passe sans erreur 500 et les données sont persistées en base.

## **Étape 6 : Préparation Front \- Le Leaderboard**

Pour préparer l'arrivée de la partie Front-End (et avoir quelque chose de visuel à afficher), nous allons créer un endpoint dédié au classement des utilisateurs.

**Objectif :** Créer une route /leaderboard qui renvoie le TOP 10 des utilisateurs triés par XP décroissant.

1. **Repository :**  
   * Ajoutez Task\<List\<User\>\> GetTopUsersAsync(int count); dans IUserRepository.  
   * Implémentez-la dans SqlUserRepository (indice : .OrderByDescending(u \=\> u.TotalXP).Take(count)).  
2. **DTO :**  
   * Créez LeaderboardResponse(int Rank, string Name, int Xp, int BadgeCount).  
   * *Note :* BadgeCount servira à afficher une icône ou un chiffre sur le front.  
3. **Endpoint :**  
   * Ajoutez GET /leaderboard dans UserEndpoints.  
   * Appelez le repository avec count \= 10\.
   * Mappez les entités vers LeaderboardResponse (le Rank peut être calculé par l'index de la boucle \+ 1).
4. **Test :**
   * Ajoutez des utilisateurs avec différents scores d'XP (en modifiant la BDD ou via un endpoint de triche temporaire).
   * Vérifiez que le JSON renvoyé est bien trié du plus fort au plus faible.