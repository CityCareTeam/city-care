# TP 2 - Infrastructure & Modélisation : Docker et SQL Server

**Module :** M-4EADL-301 - Développement Avancé & Extreme Programming

**Séance :** 2 / 8

**Focus :** Infrastructure (Docker), Modélisation de Données (Code First), EF Core.

---

## Objectif Principal

Passer d'une logique métier en mémoire (TP1) à une application capable de persister des données. Nous allons mettre en place une base de données **SQL Server** conteneurisée via **Docker** (pour éviter les installations lourdes) et préparer notre environnement pour Entity Framework Core.

---

## Prérequis

*   **Solution LevelUp :** Avoir terminé le TP1 (Le projet `LevelUp.Core` et `LevelUp.Tests` doivent exister).
*   **Docker Desktop :** Installé et lancé.
*   **Git :** Être sur une nouvelle branche pour cette séance (ex: `feature/database-setup`).

---

## Partie A : Infrastructure Moderne avec Docker

Avant de coder nos modèles, nous avons besoin d'un serveur de base de données. Plutôt que d'installer SQL Server LocalDB ou Express sur votre machine Windows (ce qui pollue le système), nous allons instancier un serveur SQL jetable et propre via Docker.

---

### Étape 1 : Récupérer l'image SQL Server

Ouvrez un terminal (PowerShell, CMD ou Terminal intégré à Visual Studio) et téléchargez la dernière image officielle de Microsoft SQL Server :

```bash
docker pull mcr.microsoft.com/mssql/server:2022-latest
```

---

### Étape 2 : Lancer le conteneur

Exécutez la commande suivante pour démarrer votre serveur de base de données.

**Attention aux points suivants :**

1.  **Mot de passe (SA\_PASSWORD) :** SQL Server exige un mot de passe fort (Majuscule + Minuscule + Chiffre + Caractère spécial + 8 caractères min). Si le mot de passe est trop simple, le conteneur s'arrêtera silencieusement.
2.  **Port (1433) :** C'est le port par défaut.

Copiez et exécutez cette commande (sur une seule ligne) :

```bash
docker run -e 'ACCEPT_EULA=Y' -e 'SA_PASSWORD=LevelUp_StrongP@ssw0rd!' -p 1433:1433 --name levelup-sql -d mcr.microsoft.com/mssql/server:2022-latest
```

Pour les ARM:
```bash
docker run --platform linux/amd64 -e 'ACCEPT_EULA=Y' -e 'SA_PASSWORD=LevelUp_StrongP@ssw0rd!' -p 1433:1433 --name levelup-sql -d mcr.microsoft.com/mssql/server:2022-latest
```

---

### Étape 3 : Vérification

Vérifiez que votre conteneur tourne correctement :

```bash
docker ps
```

  * **Si vous voyez le conteneur dans la liste :** Tout est OK.
  * **Si la liste est vide :** Faites `docker ps -a` pour voir les conteneurs arrêtés. Si `levelup-sql` est "Exited", c'est probablement que votre mot de passe n'était pas assez complexe. Supprimez le conteneur (`docker rm levelup-sql`) et recommencez l'étape 2 avec le mot de passe fourni dans l'exemple.

---

### Étape 4 : Connexion via l'IDE

Avant d'aller plus loin, assurez-vous que Visual Studio ou Rider peut se connecter à cette base.

1.  Ouvrez un explorateur de base de données
2.  Ajoutez une nouvelle connexion **SQL Server**.
3.  **Paramètres de connexion :**
      * **Host / Server name :** `localhost,1433` (ou juste `localhost`)
      * **Authentication :** SQL Server Authentication
      * **User :** `sa`
      * **Password :** `LevelUp_StrongP@ssw0rd!` (ou celui que vous avez choisi)
      * **Trust Server Certificate :** True (Cochez cette case, car le certificat SSL de Docker est auto-signé).
4.  Cliquez sur **Test Connection**.

Si le test est réussi, votre infrastructure de persistance est prête.

---
---
---

## Partie B : Modélisation Code First (Les Entités)

Maintenant que notre serveur SQL tourne dans Docker, nous devons définir **à quoi vont ressembler nos données**.

Dans l'approche **Code First**, nous n'écrivons pas de SQL (`CREATE TABLE...`). Nous écrivons des classes C\# qui *représentent* nos tables. Entity Framework se chargera de la traduction.

### Étape 1 : Organisation du Projet

Pour garder une architecture propre, nous allons séparer nos entités de la logique pure.

1.  Rendez-vous dans le projet **`LevelUp.Core`**.
2.  Créez un nouveau dossier nommé **`Entities`**.
3.  C'est dans ce dossier que nous allons créer nos classes représentant la BDD.

### Étape 2 : L'Entité "User"

L'utilisateur est au cœur du système. Il a une identité et un solde d'XP.

1.  Créez la classe `User.cs` dans le dossier `Entities`.
2.  Ajoutez les propriétés suivantes. Notez l'usage des **Data Annotations** (attributs entre `[]`) pour guider EF Core sur les contraintes SQL.

> **Note C\# :** Nous utilisons `default!` ou `required` pour gérer les avertissements de nullité (Nullable Reference Types).

```csharp
using System.ComponentModel.DataAnnotations;

namespace LevelUp.Core.Entities;

public class User
{
    [Key] // Définit la Clé Primaire (PK)
    public int Id { get; set; }

    [Required] // NOT NULL en SQL
    [MaxLength(100)] // NVARCHAR(100)
    public string Name { get; set; } = string.Empty;

    [Required]
    [EmailAddress] // Validation de format
    public string Email { get; set; } = string.Empty;

    // L'XP total accumulé (logique métier)
    public int TotalXP { get; set; } = 0;

    // Relation : Un User a plusieurs Activités (One-to-Many)
    // "virtual" est important pour le Lazy Loading (optionnel mais bonne pratique EF)
    public virtual List<Activity> Activities { get; set; } = new();

    // Relation : Un User a plusieurs Badges
    public virtual List<Badge> Badges { get; set; } = new();
}
```

### Étape 3 : L'Entité "Activity"

Une activité représente une action accomplie par l'utilisateur (ex: "A fini le TP1", "A corrigé un bug critique"). C'est l'historique de progression.

1.  Créez la classe `Activity.cs` dans `Entities`.
2.  Définissez la relation vers le `User` (Clé étrangère).

<!-- end list -->

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LevelUp.Core.Entities;

public class Activity
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string Description { get; set; } = string.Empty;

    public int XPEarned { get; set; }

    public DateTime Date { get; set; } = DateTime.UtcNow;

    // --- Clé Étrangère (Foreign Key) ---

    // 1. L'ID technique
    public int UserId { get; set; }

    // 2. La propriété de navigation (L'objet complet)
    [ForeignKey(nameof(UserId))]
    public virtual User? User { get; set; }
}
```

### Étape 4 : L'Entité "Badge"

Un badge est une récompense obtenue (ex: "Bug Hunter", "Master of TDD"). Pour simplifier ce premier modèle, nous considérerons qu'un badge appartient à un utilisateur spécifique (relation 1-N simple pour l'instant).

1.  Créez la classe `Badge.cs` dans `Entities`.

<!-- end list -->

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LevelUp.Core.Entities;

public class Badge
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty; // ex: "First Blood"

    [Required]
    public string ImageUrl { get; set; } = "default_badge.png";

    // Date d'obtention
    public DateTime AwardedOn { get; set; } = DateTime.UtcNow;

    // Relation FK vers User
    public int UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public virtual User? User { get; set; }
}
```

### Étape 5 : Vérification (Build)

À ce stade, nous avons défini le schéma de notre base de données... en pur C\#.

1.  Compilez le projet `LevelUp.Core` (Clic droit \> Build ou `Ctrl + Shift + B`).
2.  **Objectif :** 0 Erreur.
3.  Si vous avez des erreurs de namespace, vérifiez que vos fichiers commencent bien par `namespace LevelUp.Core.Entities;`.

-----

**Checkpoint :**
Votre architecture `LevelUp.Core` contient maintenant :

  * `Entities/` (User, Activity, Badge)
  * `XPCalculator.cs` (Votre logique métier du TP1)

Tout est prêt pour configurer Entity Framework et lancer la génération de la base de données.

---
---
---


## Partie C : L'Orchestration EF Core (DbContext & Migrations)

Nous avons nos entités (C#) et notre base de données (Docker). Il manque le chef d'orchestre pour faire le lien : le **DbContext**.

Pour respecter une architecture propre (Clean Architecture / Separation of Concerns), nous n'allons pas mettre le code d'accès aux données dans `LevelUp.Core` (qui doit rester pur), ni dans l'API directement. Nous allons créer une couche d'infrastructure.

### Étape 1 : Création de la couche Infrastructure

1.  Dans votre solution, ajoutez un nouveau projet de type **Class Library** nommé **`LevelUp.Infrastructure`**.
2.  Ajoutez une **référence de projet** : `LevelUp.Infrastructure` doit référencer `LevelUp.Core`.
    * *Pourquoi ?* L'infrastructure a besoin de connaître les entités (`User`, `Activity`) définies dans le Core.

### Étape 2 : Installation des NuGets

Nous devons installer Entity Framework Core dans ce nouveau projet.

1.  Ouvrez le terminal dans le dossier du projet `LevelUp.Infrastructure`.
2.  Exécutez la commande suivante :
    ```bash
    dotnet add package Microsoft.EntityFrameworkCore.SqlServer
    ```

### Étape 3 : Création du DbContext

1.  Dans `LevelUp.Infrastructure`, créez un dossier `Data`.
2.  Créez la classe `LevelUpContext.cs` à l'intérieur.
3.  Faites-la hériter de `DbContext` et exposez vos tables :

```csharp
    public DbSet<User> Users { get; set; }
    public DbSet<Activity> Activities { get; set; }
    public DbSet<Badge> Badges { get; set; }
````

### Étape 4 : Préparation de l'exécutable (API)

Pour qu'Entity Framework puisse générer des migrations, il a besoin d'un "Projet de Démarrage" (Startup Project) exécutable pour lire la configuration. Comme prévu au programme, nous allons créer la coquille de notre API Web.

1.  Ajoutez un nouveau projet à la solution : **ASP.NET Core Web API**.
2.  Nommez-le : **`LevelUp.Api`**.
3.  Ajoutez les références : `LevelUp.Api` doit référencer `LevelUp.Infrastructure`.
4.  Installez le package nécessaire pour les outils de design dans le projet **API** :
    ```bash
    dotnet add package Microsoft.EntityFrameworkCore.Design
    ```

    puis

    ```bash
    dotnet build
    ```

### Étape 5 : Configuration de la Connexion 

Nous devons dire à l'API où se trouve la base de données (Docker) et quel mot de passe utiliser.

1.  Ouvrez le fichier `appsettings.json` dans `LevelUp.Api`.
2.  Ajoutez la section `ConnectionStrings` (attention aux virgules JSON) :

<!-- end list -->

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost,1433;Database=LevelUpDb;User Id=sa;Password=LevelUp_StrongP@ssw0rd!;TrustServerCertificate=True;"
  }
}
```

> **Note :** `TrustServerCertificate=True` est obligatoire pour le développement local avec Docker (certificat auto-signé).


3.  Ouvrez `Program.cs` dans `LevelUp.Api` et configurez cela :

<!-- end list -->

```csharp
using LevelUp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// --- Configuration EF Core ---
// On récupère la chaîne de connexion depuis appsettings.json
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// On injecte le DbContext dans le conteneur de services
builder.Services.AddDbContext<LevelUpContext>(options =>
    options.UseSqlServer(connectionString));
// -----------------------------

// ... reste du code (AddEndpointsApiExplorer, etc.)
```

### Étape 6 : Générer et Appliquer la Migration

C'est le moment de vérité. Nous allons demander à EF Core de comparer nos classes C\# avec la base de données (qui est vide pour l'instant) et de générer le SQL nécessaire.

1.  **Compilez toute la solution** (Build Solution) pour vous assurer qu'il n'y a pas d'erreurs.

2.  Ouvrez un terminal à la racine de la solution (où se trouve le `.sln` ou remontez d'un niveau).

3.  Installez l'outil global (si ce n'est pas déjà fait sur votre machine) :
    ```bash
    dotnet tool install --global dotnet-ef
    ```

4.  Lancez la création de la migration :

      * `--project` : Le projet où se trouvent les migrations (Infrastructure).
      * `--startup-project` : Le projet qui lance l'app (Api).

    <!-- end list -->

    ```bash
    dotnet ef migrations add InitialCreate --project LevelUp.Infrastructure --startup-project LevelUp.Api
    ```

    *Si tout se passe bien, un dossier `Migrations` apparaît dans `LevelUp.Infrastructure`.*

5.  Appliquez la migration sur la base de données Docker :

    ```bash
    dotnet ef database update --project LevelUp.Infrastructure --startup-project LevelUp.Api
    ```

### Étape 7 : Vérification Finale

1.  Retournez dans votre explorateur de base de données.
2.  Actualisez la connexion à `localhost`.
3.  Vous devriez voir la base `LevelUpDb` et les tables `Users`, `Activities`, `Badges` (et `__EFMigrationsHistory`).


Vous avez mis en place une persistance complète **Code First** avec une architecture propre.

Votre infrastructure est prête pour le développement des fonctionnalités.
