# TP 6 - Partie A : Initialisation Front-End & Fluent UI

**Module :** M-4EADL-301 - Développement Avancé & Extreme Programming  
**Séance :** 6 / 8  
**Focus :** Blazor WebAssembly, Design Système Fluent UI, Layout & Mock Data

---

## Objectif de la Partie A

L'objectif est de créer une **Single Page Application (SPA)** moderne en utilisant la stack Microsoft. Nous allons :

1. Initialiser un projet **Blazor WebAssembly**
2. Installer la bibliothèque de composants **Fluent UI**
3. Concevoir un **Layout** professionnel (Navigation + Header)
4. Réaliser la page **Leaderboard** avec des données statiques pour valider l'affichage

---

## Étape 1 : Création du projet Blazor Client

Nous allons ajouter un nouveau projet à notre solution existante.

### 1.1 Création du projet

Dans votre terminal, à la racine de la solution :

```bash
dotnet new blazorwasm -o LevelUp.Client
```

### 1.2 Ajout à la solution

```bash
dotnet sln add LevelUp.Client/LevelUp.Client.csproj
```

---

## Étape 2 : Création du projet partagé (LevelUp.Shared)

Pour éviter la duplication de code, nous allons créer un projet de bibliothèque de classes contenant les DTOs partagés entre l'API et le Client.

> **Note :** Le projet Blazor WebAssembly ne peut pas référencer directement les projets serveur (`LevelUp.Api`) car il est compilé et exécuté dans le navigateur. Cependant, il **peut** référencer une bibliothèque de classes standard (netstandard/net) qui ne contient que des modèles de données.

### 2.1 Création du projet Shared

```bash
# Créer la bibliothèque de classes
dotnet new classlib -o LevelUp.Shared

# Ajouter à la solution
dotnet sln add LevelUp.Shared/LevelUp.Shared.csproj

# Supprimer le fichier par défaut
rm LevelUp.Shared/Class1.cs

# Créer le dossier pour les DTOs
mkdir LevelUp.Shared/Dtos
```

### 2.2 Création des DTOs partagés

Créez le fichier `LevelUp.Shared/Dtos/UserDtos.cs` :

```csharp
namespace LevelUp.Shared.Dtos;

public record CreateUserRequest(string Name, string Email);
public record GiveXpRequest(int Amount);

public record UserResponse(
    int Id,
    string Name,
    string Email,
    int Xp
);

public record LeaderboardResponse(
    int Rank,
    string Name,
    int Xp,
    int BadgeCount
);

public record UserProfileResponse(
    int Id,
    string Name,
    string Email,
    int TotalXP,
    int Level,
    List<UserBadgeResponse> Badges,
    List<ActivityResponse> RecentActivities
);
```

Créez le fichier `LevelUp.Shared/Dtos/BadgeDtos.cs` :

```csharp
namespace LevelUp.Shared.Dtos;

public record CreateBadgeRequest(string Name, string? ImageUrl, string? Description);
public record AssignBadgeRequest(int BadgeId);

public record BadgeResponse(
    int Id,
    string Name,
    string? ImageUrl
);

public record UserBadgeResponse(
    int Id,
    string Name,
    string? ImageUrl,
    DateTime AwardedOn
);
```

Créez le fichier `LevelUp.Shared/Dtos/ActivityDtos.cs` :

```csharp
namespace LevelUp.Shared.Dtos;

public record CreateActivityRequest(string Description, int XpEarned);

public record ActivityResponse(
    string Description,
    int Xp,
    DateTime Date
);
```

### 2.3 Ajout des références au projet Shared

Maintenant, référencez `LevelUp.Shared` depuis l'API et le Client :

```bash
# Référence depuis l'API
dotnet add LevelUp.Api reference LevelUp.Shared

# Référence depuis le Client
dotnet add LevelUp.Client reference LevelUp.Shared
```

### 2.4 Mise à jour de l'API (si les DTOs existaient déjà)

Si vous aviez déjà créé des DTOs dans `LevelUp.Api/Dtos/`, supprimez ce dossier et mettez à jour les imports dans les endpoints :

```bash
# Supprimer l'ancien dossier (si existant)
rm -rf LevelUp.Api/Dtos
```

Dans `LevelUp.Api/Endpoints/UserEndpoints.cs` et `BadgeEndpoints.cs`, remplacez :
```csharp
using LevelUp.Api.Dtos;
```
Par :
```csharp
using LevelUp.Shared.Dtos;
```

> **Avantage :** Les DTOs sont maintenant définis une seule fois et partagés entre l'API et le Client. Toute modification sera automatiquement reflétée des deux côtés.

---

## Étape 3 : Installation de Fluent UI

Microsoft propose une bibliothèque de composants optimisée pour Blazor : **Fluent UI Blazor**.

### 3.1 Installation des packages NuGet

⚠️ **Important :** Depuis la version 4.11.0, les icônes sont dans un package séparé.

```bash
# Package principal Fluent UI
dotnet add LevelUp.Client/LevelUp.Client.csproj package Microsoft.FluentUI.AspNetCore.Components

# Package des icônes (obligatoire pour utiliser les icônes)
dotnet add LevelUp.Client/LevelUp.Client.csproj package Microsoft.FluentUI.AspNetCore.Components.Icons
```

### 3.2 Configuration de Program.cs

Ouvrez `LevelUp.Client/Program.cs` et enregistrez les services Fluent UI :

```csharp
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.FluentUI.AspNetCore.Components;
using LevelUp.Client;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Ajout des services Fluent UI
builder.Services.AddFluentUIComponents();

await builder.Build().RunAsync();
```

### 3.3 Configuration de index.html

Dans `wwwroot/index.html`, ajoutez le CSS Fluent UI dans la balise `<head>` :

```html
<!DOCTYPE html>
<html lang="fr">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>LevelUp.Client</title>
    <base href="/" />
    <!-- CSS Fluent UI -->
    <link href="_content/Microsoft.FluentUI.AspNetCore.Components/css/reboot.css" rel="stylesheet" />
    <link rel="icon" type="image/png" href="favicon.png" />
    <link href="LevelUp.Client.styles.css" rel="stylesheet" />
</head>
<body>
    <div id="app">
        <svg class="loading-progress">
            <circle r="40%" cx="50%" cy="50%" />
            <circle r="40%" cx="50%" cy="50%" />
        </svg>
        <div class="loading-progress-text"></div>
    </div>
    <script src="_framework/blazor.webassembly.js"></script>
</body>
</html>
```

### 3.4 Configuration de _Imports.razor

Dans `_Imports.razor`, ajoutez les namespaces Fluent UI et les DTOs partagés :

```razor
@using System.Net.Http
@using System.Net.Http.Json
@using Microsoft.AspNetCore.Components.Forms
@using Microsoft.AspNetCore.Components.Routing
@using Microsoft.AspNetCore.Components.Web
@using Microsoft.AspNetCore.Components.Web.Virtualization
@using Microsoft.AspNetCore.Components.WebAssembly.Http
@using Microsoft.JSInterop
@using LevelUp.Client
@using LevelUp.Client.Layout

@* Fluent UI Components *@
@using Microsoft.FluentUI.AspNetCore.Components

@* Alias obligatoire pour les icônes depuis v4.11.0 *@
@using Icons = Microsoft.FluentUI.AspNetCore.Components.Icons

@* DTOs partagés depuis LevelUp.Shared *@
@using LevelUp.Shared.Dtos
```

> **⚠️ Point d'attention :** L'alias `Icons = Microsoft.FluentUI.AspNetCore.Components.Icons` est **obligatoire** pour utiliser les icônes. Sans cet alias, vous aurez une erreur `CS0246: Le nom de type ou d'espace de noms 'Icons' est introuvable`.

---

## Étape 4 : Mise en place du Layout (MainLayout.razor)

Nous allons remplacer le design par défaut par une interface "Fluent".

### 4.1 Modification du Layout

Ouvrez `Layout/MainLayout.razor` et remplacez le contenu :

```razor
@inherits LayoutComponentBase

<FluentLayout>
    <FluentHeader>
        <FluentStack Orientation="Orientation.Horizontal" 
                     VerticalAlignment="VerticalAlignment.Center" 
                     HorizontalGap="10">
            <FluentIcon Value="@(new Icons.Regular.Size24.Trophy())" 
                        Color="@Color.Fill" />
            <FluentLabel Typo="Typography.Header">
                LevelUp Dashboard
            </FluentLabel>
        </FluentStack>
    </FluentHeader>

    <FluentStack Orientation="Orientation.Horizontal" Width="100%">
        <FluentNavMenu Width="250" Collapsible="true" Title="Navigation">
            <FluentNavLink Href="/" 
                           Icon="@(new Icons.Regular.Size20.Home())" 
                           Match="NavLinkMatch.All">
                Accueil
            </FluentNavLink>
            <FluentNavLink Href="/leaderboard" 
                           Icon="@(new Icons.Regular.Size20.Trophy())">
                Classement
            </FluentNavLink>
            <FluentNavLink Href="/profile" 
                           Icon="@(new Icons.Regular.Size20.Person())">
                Mon Profil
            </FluentNavLink>
        </FluentNavMenu>

        <FluentBodyContent>
            <div class="content" style="padding: 20px;">
                @Body
            </div>
        </FluentBodyContent>
    </FluentStack>
</FluentLayout>

<div id="blazor-error-ui" data-nosnippet>
    An unhandled error has occurred.
    <a href="." class="reload">Reload</a>
    <span class="dismiss">🗙</span>
</div>
```

### 4.2 Suppression des fichiers CSS par défaut

Vous pouvez supprimer ou vider les fichiers CSS Bootstrap par défaut :
- `Layout/MainLayout.razor.css` (peut être vidé)
- `Layout/NavMenu.razor` et `Layout/NavMenu.razor.css` (peuvent être supprimés)

---

## Étape 5 : Page d'accueil avec statistiques

### 5.1 Modification de Home.razor

Ouvrez `Pages/Home.razor` et créez un dashboard avec des cartes :

```razor
@page "/"

<PageTitle>Accueil - LevelUp</PageTitle>

<FluentLabel Typo="Typography.PageTitle">
    Bienvenue sur LevelUp Dashboard
</FluentLabel>
<FluentDivider Style="margin: 20px 0;" />

<FluentStack Orientation="Orientation.Horizontal" 
             Wrap="true" 
             HorizontalGap="20" 
             VerticalGap="20">
    
    <!-- Carte Total Joueurs -->
    <FluentCard Style="width: 280px; padding: 20px;">
        <FluentStack Orientation="Orientation.Horizontal" 
                     VerticalAlignment="VerticalAlignment.Center" 
                     HorizontalGap="15">
            <FluentIcon Value="@(new Icons.Regular.Size32.People())" 
                        Color="@Color.Accent" />
            <FluentStack Orientation="Orientation.Vertical">
                <FluentLabel Typo="Typography.Subject">Total Joueurs</FluentLabel>
                <FluentLabel Typo="Typography.PageTitle">1,234</FluentLabel>
            </FluentStack>
        </FluentStack>
    </FluentCard>

    <!-- Carte XP Total -->
    <FluentCard Style="width: 280px; padding: 20px;">
        <FluentStack Orientation="Orientation.Horizontal" 
                     VerticalAlignment="VerticalAlignment.Center" 
                     HorizontalGap="15">
            <FluentIcon Value="@(new Icons.Regular.Size32.Star())" 
                        Color="@Color.Success" />
            <FluentStack Orientation="Orientation.Vertical">
                <FluentLabel Typo="Typography.Subject">XP Total</FluentLabel>
                <FluentLabel Typo="Typography.PageTitle">45,678</FluentLabel>
            </FluentStack>
        </FluentStack>
    </FluentCard>

    <!-- Carte Badges Distribués -->
    <FluentCard Style="width: 280px; padding: 20px;">
        <FluentStack Orientation="Orientation.Horizontal" 
                     VerticalAlignment="VerticalAlignment.Center" 
                     HorizontalGap="15">
            <FluentIcon Value="@(new Icons.Regular.Size32.Ribbon())" 
                        Color="@Color.Warning" />
            <FluentStack Orientation="Orientation.Vertical">
                <FluentLabel Typo="Typography.Subject">Badges</FluentLabel>
                <FluentLabel Typo="Typography.PageTitle">892</FluentLabel>
            </FluentStack>
        </FluentStack>
    </FluentCard>
</FluentStack>
```

---

## Étape 6 : Page Leaderboard (Données Statiques)

Nous allons créer la page de classement avec un `FluentDataGrid`.

### 6.1 Création de Leaderboard.razor

Créez le fichier `Pages/Leaderboard.razor` :

```razor
@page "/leaderboard"

<PageTitle>Classement - LevelUp</PageTitle>

<FluentLabel Typo="Typography.PageTitle">Classement Mondial</FluentLabel>
<FluentDivider Style="margin: 20px 0;" />

@if (_mockUsers == null)
{
    <FluentProgressRing>Chargement du classement...</FluentProgressRing>
}
else
{
    <FluentCard Style="padding: 0;">
        <FluentDataGrid Items="@_mockUsers.AsQueryable()" 
                        ResizableColumns="true" 
                        TGridItem="LeaderboardResponse"
                        Style="width: 100%;">
            <PropertyColumn Property="@(u => u.Rank)" 
                            Title="Rang" 
                            Sortable="true" />
            <PropertyColumn Property="@(u => u.Name)" 
                            Title="Joueur" 
                            Sortable="true" />
            <PropertyColumn Property="@(u => u.Xp)" 
                            Title="Expérience (XP)" 
                            Sortable="true" />
            <TemplateColumn Title="Badges" Align="Align.Center">
                <FluentBadge Appearance="Appearance.Accent">
                    @context.BadgeCount badges
                </FluentBadge>
            </TemplateColumn>
            <TemplateColumn Title="Action" Align="Align.End">
                <FluentButton IconEnd="@(new Icons.Regular.Size16.ChevronRight())" 
                              Appearance="Appearance.Stealth" 
                              Title="Voir le profil" />
            </TemplateColumn>
        </FluentDataGrid>
    </FluentCard>
}

@code {
    private List<LeaderboardResponse>? _mockUsers;

    protected override void OnInitialized()
    {
        // Simulation de données pour valider le design
        _mockUsers = new List<LeaderboardResponse>
        {
            new(1, "Alice Architect", 1250, 5),
            new(2, "Bob Builder", 980, 3),
            new(3, "Charlie Code", 850, 4),
            new(4, "Diana Developer", 720, 2),
            new(5, "Evan Engineer", 450, 1)
        };
    }
}
```

---

## Étape 7 : Page Profil

### 7.1 Création de Profile.razor

Créez le fichier `Pages/Profile.razor` :

```razor
@page "/profile"

<PageTitle>Mon Profil - LevelUp</PageTitle>

<FluentLabel Typo="Typography.PageTitle">Mon Profil</FluentLabel>
<FluentDivider Style="margin: 20px 0;" />

<FluentStack Orientation="Orientation.Horizontal" 
             Wrap="true" 
             HorizontalGap="30" 
             VerticalGap="20">
    
    <!-- Info utilisateur -->
    <FluentCard Style="width: 350px; padding: 20px;">
        <FluentStack Orientation="Orientation.Vertical" 
                     HorizontalAlignment="HorizontalAlignment.Center"
                     VerticalGap="15">
            <FluentPersona Name="@_mockUser.Name"
                           ImageSize="100px"
                           Initials="AA" />
            <FluentLabel Typo="Typography.Subject">@_mockUser.Email</FluentLabel>
            <FluentDivider Style="width: 100%;" />
            <FluentStack Orientation="Orientation.Horizontal" HorizontalGap="30">
                <FluentStack Orientation="Orientation.Vertical" 
                             HorizontalAlignment="HorizontalAlignment.Center">
                    <FluentLabel Typo="Typography.PageTitle">@_mockUser.Xp</FluentLabel>
                    <FluentLabel Typo="Typography.Body">XP</FluentLabel>
                </FluentStack>
                <FluentStack Orientation="Orientation.Vertical" 
                             HorizontalAlignment="HorizontalAlignment.Center">
                    <FluentLabel Typo="Typography.PageTitle">@_level</FluentLabel>
                    <FluentLabel Typo="Typography.Body">Niveau</FluentLabel>
                </FluentStack>
            </FluentStack>
        </FluentStack>
    </FluentCard>

    <!-- Badges -->
    <FluentCard Style="flex: 1; min-width: 300px; padding: 20px;">
        <FluentLabel Typo="Typography.Header" Style="margin-bottom: 15px;">
            Mes Badges
        </FluentLabel>
        <FluentStack Orientation="Orientation.Horizontal" 
                     Wrap="true" 
                     HorizontalGap="10" 
                     VerticalGap="10">
            @foreach (var badge in _mockBadges)
            {
                <FluentBadge Appearance="Appearance.Accent">
                    @badge.Name
                </FluentBadge>
            }
        </FluentStack>
    </FluentCard>
</FluentStack>

@code {
    // Données mockées pour valider le design
    private UserResponse _mockUser = new(
        Id: 1,
        Name: "Alice Architect",
        Email: "alice@levelup.dev",
        Xp: 1250
    );
    
    // Niveau calculé (100 XP par niveau)
    private int _level => _mockUser.Xp / 100 + 1;
    
    // Badges mockés (utilise BadgeResponse de LevelUp.Shared.Dtos)
    private List<BadgeResponse> _mockBadges = new()
    {
        new(1, "Early Adopter", "🌟"),
        new(2, "Code Master", "💻"),
        new(3, "Bug Hunter", "🐛"),
        new(4, "Team Player", "🤝"),
        new(5, "Speed Demon", "⚡")
    };
}
```

---

## Étape 8 : Lancement de l'application

### 8.1 Build et exécution

```bash
cd LevelUp.Client
dotnet build
dotnet run
```

### 8.2 Accès à l'application

Ouvrez votre navigateur à l'adresse : **http://localhost:5032** (ou le port affiché dans la console)

Vous devriez voir :
- Un header avec le logo LevelUp
- Un menu de navigation à gauche (Accueil, Classement, Mon Profil)
- La page d'accueil avec les cartes de statistiques

---

## Résumé des fichiers créés/modifiés

| Fichier | Description |
|---------|-------------|
| **LevelUp.Shared** | |
| `LevelUp.Shared/Dtos/UserDtos.cs` | DTOs utilisateur partagés (UserResponse, LeaderboardResponse, etc.) |
| `LevelUp.Shared/Dtos/BadgeDtos.cs` | DTOs badge partagés (BadgeResponse, UserBadgeResponse, etc.) |
| `LevelUp.Shared/Dtos/ActivityDtos.cs` | DTOs activité partagés (ActivityResponse, CreateActivityRequest) |
| **LevelUp.Client** | |
| `Program.cs` | Ajout de `AddFluentUIComponents()` |
| `_Imports.razor` | Namespaces Fluent UI + `@using LevelUp.Shared.Dtos` |
| `wwwroot/index.html` | Ajout du CSS reboot Fluent UI |
| `Layout/MainLayout.razor` | Layout Fluent UI avec header et navigation |
| `Pages/Home.razor` | Dashboard avec cartes de statistiques |
| `Pages/Leaderboard.razor` | Grille de données avec FluentDataGrid |
| `Pages/Profile.razor` | Page profil utilisateur |
| **LevelUp.Api** | |
| `Endpoints/*.cs` | Mise à jour des imports : `using LevelUp.Shared.Dtos` |

---

## Architecture du projet

```
LevelUp/
├── LevelUp.Shared/           ← Nouveau projet partagé
│   └── Dtos/
│       ├── UserDtos.cs       ← Partagé entre API et Client
│       ├── BadgeDtos.cs
│       └── ActivityDtos.cs
├── LevelUp.Api/              ← Référence LevelUp.Shared
│   └── Endpoints/
├── LevelUp.Client/           ← Référence LevelUp.Shared
│   ├── Layout/
│   └── Pages/
└── ...
```

---

## Dépannage

### Erreur : `CS0246: Le nom de type 'Icons' est introuvable`

**Cause :** Le package des icônes n'est pas installé ou l'alias n'est pas configuré.

**Solution :**
1. Installez le package : `dotnet add package Microsoft.FluentUI.AspNetCore.Components.Icons`
2. Ajoutez l'alias dans `_Imports.razor` : `@using Icons = Microsoft.FluentUI.AspNetCore.Components.Icons`

### Erreur : Les styles ne s'appliquent pas

**Cause :** Le CSS Fluent UI n'est pas chargé.

**Solution :** Vérifiez que `reboot.css` est bien référencé dans `index.html`.

---

## Challenge Autonomie

**Mission : Personnalisation avancée**

1. **Thème sombre :** Ajoutez le composant `<FluentDesignTheme Mode="DesignThemeModes.Dark" />` dans votre layout pour activer le mode sombre.

2. **Barre de progression XP :** Dans la page profil, ajoutez un `<FluentProgressBar>` montrant la progression vers le niveau suivant.

3. **Recherche :** Ajoutez un champ `<FluentSearch>` dans le header pour rechercher un joueur.

---

## Prochaine étape

Dans la **Partie B**, nous connecterons notre interface à l'API :
- Configuration du `HttpClient` pour appeler l'API
- Authentification avec Keycloak (OIDC)
- Affichage des vraies données depuis la base de données