# TP 3 - Partie A : Architecture API, DTOs & Persistance SQL

**Module :** M-4EADL-301 - Développement Avancé & Extreme Programming

**Séance :** 3 / 8

**Focus :** Architecture Découplée, Repository Pattern, Injection de Dépendance (IoC).

---

## Objectif de la Partie A

Actuellement, notre couche `Infrastructure` (EF Core) fonctionne et notre base de données Docker est prête (TP 2). 

Cependant, notre API n'est pas encore câblée correctement.

Au lieu d'utiliser le `DbContext` directement dans l'API (ce qui créerait un couplage fort), nous allons mettre en place une **architecture propre** basée sur le **Repository Pattern**.

Nous allons :
1.  Définir le contrat (`Interface`) dans le coeur du projet.
2.  Implémenter la mécanique SQL dans l'Infrastructure.
3.  Configurer le moteur d'Injection de Dépendance (IoC) pour relier le tout.

---

### Étape 1 : Le Contrat (Couche Core)

Nous commençons par définir **ce que** nous voulons faire, sans nous soucier de **comment** (SQL, Fichier, etc.).

1.  Dans le projet **`LevelUp.Core`**, créez un dossier **`Interfaces`**.
2.  Ajoutez une interface nommée `IUserRepository.cs`.
3.  Définissez les signatures suivantes. Notez l'utilisation obligatoire de `Task` pour l'asynchronisme.

```csharp
using LevelUp.Core.Entities;

namespace LevelUp.Core.Interfaces;

public interface IUserRepository
{
    // Récupérer tous les utilisateurs
    Task<IEnumerable<User>> GetAllAsync();

    // Récupérer un utilisateur par son ID (peut être null)
    Task<User?> GetByIdAsync(int id);

    // Ajouter un nouvel utilisateur et retourner l'entité créée (avec son nouvel ID)
    Task<User> AddAsync(User user);

    // Sauvegarder les changements (Pattern Unit of Work simplifié)
    // Dans une architecture plus complexe, cela serait dans une interface IUnitOfWork séparée.
    Task SaveChangesAsync();
}

```

> **Question d'architecture :** Pourquoi cette interface est-elle dans `Core` ? 

Parce que c'est une règle métier : "L'application *doit* pouvoir gérer des utilisateurs". 
C'est indépendant de la base de données technique.

---

### Étape 2 : L'Implémentation SQL (Couche Infrastructure)

Maintenant, nous devons réaliser ce contrat en utilisant Entity Framework Core.

1. Dans le projet **`LevelUp.Infrastructure`**, créez un dossier **`Repositories`**.
2. Créez la classe `UserRepository.cs` qui implémente `IUserRepository`.
3. Vous allez devoir injecter le `LevelUpContext` via le constructeur.

```csharp
using LevelUp.Core.Entities;
using LevelUp.Core.Interfaces;
using LevelUp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LevelUp.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly LevelUpContext _context;

    // INJECTION DE DÉPENDANCE (Constructeur)
    // On demande le DbContext, on ne le crée pas avec "new" !
    public UserRepository(LevelUpContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<User>> GetAllAsync()
    {
        // Utilisation de AsNoTracking() pour la lecture seule (Performance ++)
        return await _context.Users.AsNoTracking().ToListAsync();
    }

    public async Task<User?> GetByIdAsync(int id)
    {
        return await _context.Users.FindAsync(id);
    }

    public async Task<User> AddAsync(User user)
    {
        await _context.Users.AddAsync(user);
        // Note : On ne fait pas SaveChanges ici pour laisser le choix du moment de la transaction
        // On ajoute l'entité au DbContext, mais la requête SQL INSERT n'est pas encore partie
        return user;
    }

    public async Task SaveChangesAsync()
    {
      // C'est ici que la transaction SQL est exécutée (COMMIT)
        await _context.SaveChangesAsync();
    }
}

```

---

### Étape 3 : Le Câblage IoC (Couche API)

C'est l'étape critique. Si vous oubliez ça, l'API plantera au démarrage avec une erreur du type *"Unable to resolve service..."*.

1. Ouvrez le fichier `Program.cs` dans le projet **`LevelUp.Api`**.
2. Repérez la ligne où vous avez configuré le `DbContext` (TP2).
3. Juste en dessous, enregistrez votre Repository dans le conteneur de services.

**Le choix du Cycle de Vie :**
Le `DbContext` est enregistré en **Scoped** par défaut. Notre Repository dépend du `DbContext`. Il **doit** donc être **Scoped** lui aussi.

```csharp
using LevelUp.Core.Interfaces;
using LevelUp.Infrastructure.Repositories;
// ... autres usings

var builder = WebApplication.CreateBuilder(args);

// [Config Existante] DB Context
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<LevelUpContext>(options =>
    options.UseSqlServer(connectionString));

// Injection des Repositories
// "Quand on te demande IUserRepository, fournis une instance de UserRepository"
builder.Services.AddScoped<IUserRepository, UserRepository>();

var app = builder.Build();

```

---

### Étape 4 : Vérification (Smoke Test)

Pour vérifier que le câblage fonctionne sans avoir encore créé de contrôleur, nous allons faire un test rapide dans le `Program.cs`.

Ajoutez ce petit bout de code temporaire juste avant `app.Run()` :

```csharp
// TEST TEMPORAIRE (À supprimer après validation)
using (var scope = app.Services.CreateScope())
{
    var repo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
    Console.WriteLine($"Repository injecté avec succès : {repo.GetType().Name}");
}

app.Run();

```

1. Lancez l'application (`LevelUp.Api`).
2. Regardez la console de débogage. Si vous voyez **"Repository injecté avec succès : UserRepository"**, bravo ! L'Inversion de Contrôle est en place.



### Étape 5 : Non Guidé - Gestion des Badges

Vous avez vu comment câbler l'entité `User`. À vous de reproduire cette architecture pour l'entité **`Badge`**.
Cependant, le besoin métier est différent. Pour les badges, nous avons besoin de requêtes plus spécifiques.

**Votre mission :**

1.  **Architecture (Core) :**
    * Créez l'interface `IBadgeRepository`.
    * Elle **DOIT** contenir une méthode pour récupérer tous les badges d'un utilisateur spécifique : `GetBadgesByUserIdAsync(int userId)`.
    * Elle doit permettre d'ajouter un badge (`AddAsync`).

2.  **Infrastructure (SQL) :**
    * Implémentez `SqlBadgeRepository`.

3.  **Configuration (API) :**
    * Enregistrez ce nouveau service dans l'IoC (`Program.cs`)
    * *Question :* Quel cycle de vie devez-vous utiliser et pourquoi ?

4.  **Validation :**
    * Modifiez votre *Smoke Test* (Étape 4) pour demander au conteneur une instance de `IBadgeRepository`.
    * Si l'application démarre et affiche que le repository des badges est injecté, vous avez réussi.

> **Pourquoi ce challenge ?**
> Dans la vraie vie, vous passerez votre temps à créer de nouveaux Repositories pour de nouvelles tables. Ce pattern doit devenir un réflexe.


### ⚠️ AVERTISSEMENT : L'illusion de la compétence par l'IA

> **"L'IA peut écrire le code à votre place, mais elle ne peut pas réfléchir à votre place."**

Dans ce module, nous abordons des concepts d'architecture avancée (Repository Pattern, IoC, TDD). L'objectif n'est pas seulement de *produire* du code qui marche, mais de **comprendre** pourquoi il est structuré ainsi.

**Pourquoi je vous déconseille fortement de l'utiliser pour ce TP :**

1. **Le piège du "Copier-Coller" architectural :** Les IA génèrent souvent du code qui fonctionne dans l'immédiat mais qui viole les principes d'architecture (couplage fort, logique métier dans les contrôleurs). Si vous copiez sans comprendre, vous construisez sur du sable.
2. **L'atrophie de la réflexion :** Le défi de ce TP est de *concevoir* l'interaction entre les couches (Core -> Infra). Si l'IA le fait pour vous, vous manquez l'exercice mental crucial qui transforme un développeur junior en architecte.
3. **La maintenance future :** Le code généré par IA est souvent verbeux ou utilise des patterns obsolètes. Vous serez seuls face à votre code lors de l'examen et en entreprise.

**Ma recommandation :**
Utilisez l'IA comme un **mentor**, pas comme un sous-traitant.

* **Bon :** "Explique-moi la différence entre Scoped et Singleton."
* **Bon :** "Pourquoi mon test unitaire échoue avec cette erreur ?"
* **Mauvais :** "Écris-moi le Repository pour les Badges."

**Le vrai test :** Si vous ne pouvez pas expliquer chaque ligne de votre `Program.cs` ou de votre Repository sans regarder l'écran, c'est que vous n'avez pas appris.
