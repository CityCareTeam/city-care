# **TP 4 : Services, Business Logic & TDD Avancé**

**Module :** M-4EADL-301 \- Développement Avancé & Extreme Programming

**Séance :** 4 / 8

**Focus :** Service Layer Pattern, Inversion de Dépendance, Mocking (NSubstitute).

## **Objectif du TP**

À la fin du TP 3, notre code commençait à "sentir mauvais" (*Code Smell*). Nous avons injecté de la logique métier (calcul d'XP, vérification de badges) directement dans les Repositories ou les Endpoints.

**Problèmes identifiés :**

1. **Mélange des responsabilités :** Un Repository doit gérer le SQL, pas décider si un utilisateur mérite un badge.  
2. **Tests difficiles :** Pour tester une règle métier, nous sommes obligés de lancer Docker et SQL Server. C'est lent et fragile.

**Aujourd'hui, nous allons :**

* Implémenter la couche **Service** pour isoler le métier.  
* Utiliser le **Mocking** pour tester notre logique en microsecondes, sans base de données.

## **Étape 1 : Définir le Contrat du Service (Couche Core)**

Le Service est un "Chef d'Orchestre". Il définit des actions métier claires.

1. Dans le projet **LevelUp.Core**, créez un dossier **Services** s'il n'existe pas.  
2. Dans le dossier **Interfaces**, créez l'interface IUserXpService.cs.

```csharp
namespace LevelUp.Core.Interfaces;

public interface IUserXpService  
{  
    /// \<summary\>  
    /// Ajoute une activité à un utilisateur, met à jour son XP total   
    /// et vérifie s'il est éligible à de nouvelles récompenses (badges).  
    /// \</summary\>  
    Task ProcessNewActivityAsync(int userId, string description, int xpAmount);  
}
```

## **Étape 2 : Implémentation de la Logique Métier**

C'est ici que réside l'intelligence de votre application. Le service dépend des interfaces des repositories, jamais des classes concrètes.

1. Dans le projet **LevelUp.Core**, créez la classe UserXpService.cs.  
2. **Refactoring :** Déplacez la logique d'attribution automatique du badge "Vétéran" (vue au TP 3\) ici.

```csharp
using LevelUp.Core.Entities;  
using LevelUp.Core.Interfaces;

namespace LevelUp.Core.Services;

public class UserXpService : IUserXpService  
{  
    private readonly IUserRepository \_userRepo;  
    private readonly IBadgeRepository \_badgeRepo;

    public UserXpService(IUserRepository userRepo, IBadgeRepository badgeRepo)  
    {  
        \_userRepo \= userRepo;  
        \_badgeRepo \= badgeRepo;  
    }

    public async Task ProcessNewActivityAsync(int userId, string description, int xpAmount)  
    {  
        // 1\. Récupération de l'utilisateur avec ses relations (Eager Loading via Repo)  
        var user \= await \_userRepo.GetUserProfileAsync(userId);  
        if (user \== null) throw new Exception("Utilisateur introuvable");

        // 2\. Logique Métier : Mise à jour de l'état  
        user.TotalXP \+= xpAmount;  
          
        var activity \= new Activity   
        {   
            Description \= description,   
            XPEarned \= xpAmount,   
            Date \= DateTime.UtcNow   
        };  
        user.Activities.Add(activity);

        // 3\. Logique Métier : Règle de récompense automatique  
        if (user.TotalXP \>= 500 && \!user.Badges.Any(b \=\> b.Name \== "Vétéran"))  
        {  
            // Note : On pourrait chercher le badge existant via \_badgeRepo  
            user.Badges.Add(new Badge { Name \= "Vétéran", Description \= "A franchi les 500 XP" });  
        }

        // 4\. Persistance UNIQUE (Atomicité)  
        // Le service décide quand la transaction est terminée.  
        await \_userRepo.SaveChangesAsync();  
    }  
}
```

## **Étape 3 : Câblage IoC et Nettoyage de l'API**

Il faut maintenant déclarer notre nouveau service et l'utiliser dans nos routes.

1. Dans le Program.cs de LevelUp.Api, enregistrez le service :  
   builder.Services.AddScoped\<IUserXpService, UserXpService\>();  
2. Dans UserEndpoints.cs, modifiez la route POST /users/{userId}/activities pour utiliser le service au lieu du repository.

```csharp
// L'Endpoint ne contient plus de logique, il délègue au service.  
group.MapPost("/{userId}/activities", async (int userId, CreateActivityRequest request, IUserXpService xpService) \=\>  
{  
    await xpService.ProcessNewActivityAsync(userId, request.Description, request.XpEarned);  
    return Results.Ok(new { Message \= "Activité enregistrée et récompenses traitées." });  
});
```

## **Étape 4 : TDD Avancé \- L'Art du Mocking (NSubstitute)**

Nous allons tester que notre service attribue bien le badge "Vétéran" sans jamais toucher à SQL Server.

1. Dans votre projet de Tests, installez le package : dotnet add package NSubstitute.  
2. Créez une classe UserXpServiceTests.cs.

### **Exemple de Test unitaire avec Mock (Pattern AAA)**
```csharp
using NSubstitute;  
using LevelUp.Core.Entities;  
using LevelUp.Core.Interfaces;  
using LevelUp.Core.Services;  
using Xunit;

public class UserXpServiceTests  
{  
    \[Fact\]  
    public async Task ProcessActivity\_Should\_AddBadge\_When\_XpThresholdReached()  
    {  
        // \--- ARRANGE \---  
        // On crée des "doublures" (Mocks)  
        var userRepo \= Substitute.For\<IUserRepository\>();  
        var badgeRepo \= Substitute.For\<IBadgeRepository\>();  
          
        // On prépare un utilisateur qui va basculer au dessus de 500 XP  
        var fakeUser \= new User { Id \= 1, TotalXP \= 450, Badges \= new List\<Badge\>() };  
          
        // On configure le Mock pour renvoyer notre faux utilisateur  
        userRepo.GetUserProfileAsync(1).Returns(fakeUser);  
          
        var service \= new UserXpService(userRepo, badgeRepo);

        // \--- ACT \---  
        await service.ProcessNewActivityAsync(1, "Boss Final", 60);

        // \--- ASSERT \---  
        // 1\. Vérification de l'état  
        Assert.Equal(510, fakeUser.TotalXP);  
        Assert.Contains(fakeUser.Badges, b \=\> b.Name \== "Vétéran");

        // 2\. Vérification du comportement (Le service a-t-il bien sauvegardé ?)  
        await userRepo.Received(1).SaveChangesAsync();  
    }  
}
```

## **Étape 5 : Challenge Autonomie**

**Mission : Robustesse et Non-Doublon**

1. **Test de non-doublon :** Écrivez un test qui prouve que si l'utilisateur possède déjà le badge "Vétéran", le service ne lui en ajoute pas un deuxième (la liste Badges ne doit contenir qu'un seul élément "Vétéran").  
2. **Règle métier Bonus :** Implémentez une nouvelle règle dans UserXpService : Si la description de l'activité contient le mot "SECRET", l'XP gagné est doublé.  
3. **TDD :** Écrivez le test unitaire *avant* d'implémenter cette règle (Red/Green/Refactor).

### **Pourquoi est-ce "Pro" ?**

* Vos tests s'exécutent en **quelques millisecondes**.  
* Vous pouvez tester des scénarios complexes (erreurs réseau, données corrompues) en configurant vos Mocks.  
* Votre API est devenue une simple "coquille" facile à maintenir.