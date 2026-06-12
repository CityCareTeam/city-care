# TP 6 - Partie B : Intégration API & Sécurité Keycloak

**Module :** M-4EADL-301 - Développement Avancé & Extreme Programming  
**Séance :** 6 / 8  
**Focus :** CORS, HttpClient, Authentification OIDC (Keycloak), Injection de Token

---

## Objectif de la Partie B

Dans cette étape finale du TP 6, nous allons :

1. Autoriser le Front-End à appeler l'API (**Configuration CORS**)
2. Connecter Blazor à **Keycloak** pour permettre le Login/Logout
3. Automatiser l'envoi du **Jeton JWT** dans les appels API
4. Remplacer les données "mockées" par les données réelles du **Leaderboard**

---

## Prérequis

Avant de commencer, assurez-vous que :
- Le TP5 est complété (Keycloak configuré avec le realm `LevelUp`)
- Docker Compose est lancé (`docker compose up -d`)
- L'API `LevelUp.Api` est fonctionnelle sur `http://localhost:5207`
- Le client `LevelUp.Client` (TP6 Partie A) est créé

---

## Etape 0 : Configuration Keycloak pour le Client Blazor

Le client Blazor WebAssembly doit etre declare dans Keycloak avec les bonnes URLs de redirection.

### 0.1 Fichier de requêtes HTTP

Créez le fichier `LevelUp.Api/Keycloak.http` pour configurer Keycloak depuis VS Code :

```http
### ============================================================
### Configuration Keycloak pour LevelUp
### ============================================================

### 1. Obtenir un token admin Keycloak
# @name getAdminToken
POST http://localhost:8080/realms/master/protocol/openid-connect/token
Content-Type: application/x-www-form-urlencoded

username=admin&password=admin&grant_type=password&client_id=admin-cli

###

### 2. Lister tous les clients du realm LevelUp
# @name listClients
GET http://localhost:8080/admin/realms/LevelUp/clients
Authorization: Bearer {{getAdminToken.response.body.access_token}}

###

### 3. Configurer le client levelup-api pour le front-end Blazor
### IMPORTANT: Executez d'abord les requetes 1 et 2
@clientUuid = {{listClients.response.body.[?(@.clientId=='levelup-api')].id}}

PUT http://localhost:8080/admin/realms/LevelUp/clients/{{clientUuid}}
Authorization: Bearer {{getAdminToken.response.body.access_token}}
Content-Type: application/json

{
    "clientId": "levelup-api",
    "name": "LevelUp API & Client",
    "publicClient": true,
    "directAccessGrantsEnabled": true,
    "standardFlowEnabled": true,
    "redirectUris": [
        "http://localhost:5207/*",
        "http://localhost:5032/*"
    ],
    "webOrigins": [
        "http://localhost:5207",
        "http://localhost:5032",
        "*"
    ],
    "attributes": {
        "pkce.code.challenge.method": "S256"
    }
}

###

### 4. Verifier la configuration du client
GET http://localhost:8080/admin/realms/LevelUp/clients/{{clientUuid}}
Authorization: Bearer {{getAdminToken.response.body.access_token}}
```

### 0.2 Exécution des requêtes

1. Ouvrez `LevelUp.Api/Keycloak.http` dans VS Code
2. Cliquez sur **"Send Request"** pour la requête 1 (token admin)
3. Cliquez sur **"Send Request"** pour la requête 2 (liste des clients)
4. Cliquez sur **"Send Request"** pour la requête 3 (configuration du client)
5. Cliquez sur **"Send Request"** pour la requête 4 (vérification)

Résultat attendu pour la requête 4 :
```json
{
  "clientId": "levelup-api",
  "publicClient": true,
  "redirectUris": ["http://localhost:5207/*", "http://localhost:5032/*"],
  "webOrigins": ["http://localhost:5207", "http://localhost:5032", "*"]
}
```

### 0.3 Configuration manuelle (alternative)

Si vous préférez configurer manuellement :

1. Connectez-vous à **http://localhost:8080** (admin / admin)
2. Sélectionnez le Realm **LevelUp**
3. Allez dans **Clients** → **levelup-api**
4. Dans **Settings** :
   - **Client authentication** : **OFF**
   - **Valid redirect URIs** : `http://localhost:5032/*`
   - **Web Origins** : `http://localhost:5032`
5. Cliquez sur **Save**

---

## Etape 1 : Configuration CORS (Cote API)

Par sécurité, les navigateurs bloquent les appels entre deux domaines différents (ex: de `localhost:5032` vers `localhost:5207`). C'est le mécanisme **CORS** (Cross-Origin Resource Sharing).

### 1.1 Ajout de la politique CORS

Dans `LevelUp.Api/Program.cs`, ajoutez la configuration CORS **avant** `builder.Build()` :

```csharp
// ==================== CONFIGURATION CORS (TP6) ====================
// Cross-Origin Resource Sharing : Autorise le front-end Blazor à appeler l'API
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
                "http://localhost:5032",    // Blazor HTTP
                "https://localhost:7236"    // Blazor HTTPS
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();  // Nécessaire pour l'envoi des cookies/tokens
    });
});
```

### 1.2 Activation du middleware CORS

Ajoutez le middleware **après** `UseHttpsRedirection()` mais **avant** `UseAuthentication()` :

```csharp
app.UseHttpsRedirection();

// ==================== MIDDLEWARE CORS (TP6) ====================
// ORDRE CRITIQUE ! UseCors doit être avant UseAuthentication
app.UseCors();

app.UseAuthentication();
app.UseAuthorization();
```

> **⚠️ Ordre des middlewares :** L'ordre est crucial ! CORS doit être traité avant l'authentification pour que les requêtes preflight (OPTIONS) passent correctement.

---

## Étape 2 : Installation des packages de sécurité (Côté Client)

### 2.1 Installation du package OIDC

Dans le terminal, à la racine du projet Client :

```bash
cd LevelUp.Client
dotnet add package Microsoft.AspNetCore.Components.WebAssembly.Authentication
```

Ce package fournit :
- `AddOidcAuthentication()` : Configuration OpenID Connect
- `AuthorizationMessageHandler` : Injection automatique du token JWT sur les URLs autorisées
- `AuthorizeRouteView` : Protection des routes Blazor
- `RemoteAuthenticatorView` : Gestion du flow de login/logout

---

## Étape 3 : Configuration de l'authentification (Program.cs)

### 3.1 Modification de Program.cs

Remplacez le contenu de `LevelUp.Client/Program.cs` :

```csharp
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.FluentUI.AspNetCore.Components;
using LevelUp.Client;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// ==================== CONFIGURATION HTTP CLIENT AVEC TOKEN JWT ====================
// URL de base de l'API
var apiBaseUrl = "http://localhost:5207";

// HttpClient qui injecte automatiquement le token Bearer sur les requêtes vers l'API
builder.Services.AddHttpClient("LevelUp.API", client =>
    {
        client.BaseAddress = new Uri(apiBaseUrl);
    })
    .AddHttpMessageHandler(sp =>
    {
        // Configuration du handler pour envoyer le token uniquement vers l'API
        var handler = sp.GetRequiredService<AuthorizationMessageHandler>()
            .ConfigureHandler(
                authorizedUrls: new[] { apiBaseUrl },
                scopes: new[] { "openid", "profile" }
            );
        return handler;
    });

// Client HTTP par défaut (pour les composants qui injectent HttpClient)
builder.Services.AddScoped(sp => 
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("LevelUp.API"));

// ==================== CONFIGURATION OIDC KEYCLOAK ====================
builder.Services.AddOidcAuthentication(options =>
{
    // Chargement de la configuration depuis wwwroot/appsettings.json
    builder.Configuration.Bind("Keycloak", options.ProviderOptions);
    
    // Type de réponse OAuth2 : Authorization Code Flow (recommandé pour SPA)
    options.ProviderOptions.ResponseType = "code";
    
    // Mapping de la claim contenant les rôles Keycloak
    options.UserOptions.RoleClaim = "roles";
});

// Enregistrement des services Fluent UI
builder.Services.AddFluentUIComponents();

await builder.Build().RunAsync();
```

> **Note importante :** On utilise `AuthorizationMessageHandler.ConfigureHandler()` pour spécifier explicitement l'URL de l'API autorisée à recevoir le token JWT. Sans cette configuration, le token ne serait pas envoyé.

### 3.2 Création du fichier de configuration

Créez `LevelUp.Client/wwwroot/appsettings.json` :

```json
{
  "Keycloak": {
    "Authority": "http://localhost:8080/realms/LevelUp",
    "ClientId": "levelup-api",
    "ResponseType": "code"
  }
}
```

> **Note :** L'`Authority` doit correspondre exactement à l'URL de votre realm Keycloak. Le `ClientId` doit correspondre au client configuré dans Keycloak (TP5).

---

## Étape 4 : Mise à jour de _Imports.razor

Ajoutez les namespaces d'authentification dans `_Imports.razor` :

```razor
@using System.Net.Http
@using System.Net.Http.Json
@using Microsoft.AspNetCore.Components.Forms
@using Microsoft.AspNetCore.Components.Routing
@using Microsoft.AspNetCore.Components.Web
@using Microsoft.AspNetCore.Components.Web.Virtualization
@using Microsoft.AspNetCore.Components.WebAssembly.Http
@using Microsoft.AspNetCore.Components.Authorization
@using Microsoft.AspNetCore.Components.WebAssembly.Authentication
@using Microsoft.JSInterop
@using Microsoft.FluentUI.AspNetCore.Components
@using Icons = Microsoft.FluentUI.AspNetCore.Components.Icons
@using LevelUp.Client
@using LevelUp.Client.Layout
@using LevelUp.Client.Shared
@using LevelUp.Shared.Dtos
```

> **Note :** On utilise `LevelUp.Shared.Dtos` car les DTOs sont partagés entre l'API et le Client via le projet `LevelUp.Shared` (créé en Partie A).

---

## Etape 5 : Adaptation de App.razor

Remplacez le contenu de `App.razor` pour activer le contexte d'authentification :

```razor
<CascadingAuthenticationState>
    <Router AppAssembly="@typeof(App).Assembly" NotFoundPage="typeof(Pages.NotFound)">
        <Found Context="routeData">
            <RouteView RouteData="@routeData" DefaultLayout="@typeof(MainLayout)" />
            <FocusOnNavigate RouteData="@routeData" Selector="h1" />
        </Found>
    </Router>
</CascadingAuthenticationState>
```

> **Note :** On utilise `RouteView` au lieu de `AuthorizeRouteView` pour permettre la navigation anonyme. L'authentification sera geree par les composants `<AuthorizeView>` dans le layout et les pages.

### 5.1 Creation du composant RedirectToLogin

Creez `LevelUp.Client/Shared/RedirectToLogin.razor` :

```razor
@inject NavigationManager Navigation

@code {
    protected override void OnInitialized()
    {
        Navigation.NavigateToLogin("authentication/login");
    }
}
```

### 5.2 Création de la page d'authentification

Créez `LevelUp.Client/Pages/Authentication.razor` :

```razor
@page "/authentication/{action}"
@using Microsoft.AspNetCore.Components.WebAssembly.Authentication

<RemoteAuthenticatorView Action="@Action">
    <LogInFailed>
        <FluentCard Style="padding: 20px; max-width: 400px; margin: 50px auto;">
            <FluentStack Orientation="Orientation.Vertical" 
                         HorizontalAlignment="HorizontalAlignment.Center">
                <FluentIcon Value="@(new Icons.Regular.Size32.DismissCircle())" 
                            Color="@Color.Error" />
                <FluentLabel Typo="Typography.Header">Échec de connexion</FluentLabel>
                <FluentLabel>Une erreur s'est produite lors de la connexion.</FluentLabel>
                <FluentButton Appearance="Appearance.Accent" 
                              OnClick="@(() => Navigation.NavigateTo("/"))">
                    Retour à l'accueil
                </FluentButton>
            </FluentStack>
        </FluentCard>
    </LogInFailed>
    <LogOutSucceeded>
        <FluentCard Style="padding: 20px; max-width: 400px; margin: 50px auto;">
            <FluentStack Orientation="Orientation.Vertical" 
                         HorizontalAlignment="HorizontalAlignment.Center">
                <FluentIcon Value="@(new Icons.Regular.Size32.Checkmark())" 
                            Color="@Color.Success" />
                <FluentLabel Typo="Typography.Header">Déconnecté</FluentLabel>
                <FluentLabel>Vous avez été déconnecté avec succès.</FluentLabel>
                <FluentButton Appearance="Appearance.Accent" 
                              OnClick="@(() => Navigation.NavigateTo("/"))">
                    Retour à l'accueil
                </FluentButton>
            </FluentStack>
        </FluentCard>
    </LogOutSucceeded>
</RemoteAuthenticatorView>

@code {
    [Parameter] public string? Action { get; set; }
    [Inject] private NavigationManager Navigation { get; set; } = default!;
}
```

---

## Étape 6 : Mise à jour du Layout avec design moderne

Dans la Partie A, nous avons utilisé les composants `FluentLayout`, `FluentNavMenu` et `FluentNavLink` pour créer rapidement un layout fonctionnel. 

Dans cette partie, nous allons **améliorer significativement le design** avec :
- Un header moderne avec gradient et boutons de connexion
- Une sidebar sombre personnalisée
- Des animations et transitions

> **⚠️ Pourquoi ne pas utiliser FluentNavLink ?**  
> Les composants `FluentNavMenu` et `FluentNavLink` utilisent le Shadow DOM, ce qui rend impossible la personnalisation des couleurs de texte sur fond sombre via CSS. Nous utilisons donc le composant Blazor natif `NavLink` avec nos propres styles.

### 6.1 Styles CSS personnalisés

Ajoutez les styles dans `wwwroot/css/app.css` :

```css
/* ==================== STYLES LEVELUP ==================== */

/* Layout principal */
.app-layout {
    display: flex;
    flex-direction: column;
    height: 100vh;
    overflow: hidden;
}

/* Header */
.app-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
    padding: 0 24px;
    height: 64px;
    box-shadow: 0 4px 20px rgba(102, 126, 234, 0.3);
    flex-shrink: 0;
}

.header-brand {
    display: flex;
    align-items: center;
    gap: 12px;
}

.brand-icon {
    background: rgba(255,255,255,0.2);
    border-radius: 12px;
    padding: 8px;
    display: flex;
    align-items: center;
    justify-content: center;
}

.brand-title {
    color: white;
    font-weight: 700;
    font-size: 1.4rem;
}

.brand-badge {
    background: rgba(255,255,255,0.2);
    color: white;
    font-size: 0.65rem;
    padding: 4px 8px;
    border-radius: 4px;
    font-weight: 600;
}

.header-user {
    display: flex;
    align-items: center;
    gap: 12px;
}

.user-avatar {
    background: rgba(255,255,255,0.2);
    border-radius: 50%;
    padding: 6px;
    display: flex;
}

.user-name {
    color: white;
    font-weight: 500;
    white-space: nowrap;
    max-width: 200px;
    overflow: hidden;
    text-overflow: ellipsis;
}

/* Header Buttons */
.header-btn {
    display: flex;
    align-items: center;
    gap: 8px;
    cursor: pointer;
    font-size: 0.9rem;
    border: none;
    transition: all 0.2s ease;
}

.logout-btn {
    background: rgba(255, 255, 255, 0.15);
    color: white;
    border-radius: 8px;
    padding: 8px 16px;
    font-weight: 500;
}

.logout-btn:hover {
    background: rgba(255, 255, 255, 0.25);
}

.login-btn {
    background: white;
    color: #667eea;
    border-radius: 8px;
    padding: 10px 20px;
    font-weight: 600;
    box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
}

.login-btn:hover {
    transform: translateY(-2px);
    box-shadow: 0 6px 16px rgba(0, 0, 0, 0.2);
}

/* Body */
.app-body {
    display: flex;
    flex: 1;
    overflow: hidden;
}

/* Sidebar */
.sidebar {
    width: 260px;
    background: linear-gradient(180deg, #1a1a2e 0%, #16213e 100%);
    padding: 20px 0;
    box-shadow: 4px 0 20px rgba(0,0,0,0.1);
    overflow-y: auto;
    flex-shrink: 0;
}

.sidebar-section-title {
    color: rgba(255,255,255,0.5);
    font-size: 0.7rem;
    text-transform: uppercase;
    letter-spacing: 1.5px;
    padding: 0 20px 16px 20px;
    margin-bottom: 8px;
}

.sidebar-admin-separator {
    margin-top: 24px;
    padding-top: 24px;
    border-top: 1px solid rgba(255,255,255,0.1);
}

.nav-item {
    display: flex;
    align-items: center;
    gap: 12px;
    padding: 12px 20px;
    margin: 4px 12px;
    border-radius: 10px;
    color: rgba(255, 255, 255, 0.85);
    text-decoration: none;
    font-size: 0.95rem;
    font-weight: 500;
    transition: all 0.2s ease;
}

.nav-item:hover {
    background: rgba(102, 126, 234, 0.25);
    color: white;
}

.nav-item.active {
    background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
    color: white;
    box-shadow: 0 4px 15px rgba(102, 126, 234, 0.4);
}

/* Main Content */
.main-content {
    flex: 1;
    padding: 32px 40px;
    overflow-y: auto;
    background: linear-gradient(135deg, #f5f7fa 0%, #e4e8ec 100%);
}

/* ==================== PAGES ==================== */

/* Page Header */
.page-header {
    margin-bottom: 32px;
}

.page-header-row {
    display: flex;
    align-items: center;
    gap: 12px;
}

.page-header-icon {
    background: linear-gradient(135deg, #FFD700 0%, #FFA500 100%);
    border-radius: 12px;
    padding: 10px;
    display: flex;
    align-items: center;
    justify-content: center;
}

.page-title {
    font-weight: 700;
    color: #1a1a2e;
    margin: 0;
}

.page-subtitle {
    color: #666;
    font-size: 0.9rem;
}

/* Home Page */
.home-title {
    font-size: 2.5rem;
    font-weight: 800;
    background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
    -webkit-background-clip: text;
    -webkit-text-fill-color: transparent;
    background-clip: text;
    margin-bottom: 8px;
}

.home-subtitle {
    font-size: 1.1rem;
    color: #666;
    max-width: 600px;
}

.loading-container {
    padding: 40px;
    background: white;
    border-radius: 16px;
    box-shadow: 0 4px 20px rgba(0,0,0,0.08);
    display: flex;
    align-items: center;
    gap: 16px;
}

.loading-text {
    color: #667eea;
    font-weight: 500;
}

.stats-grid {
    margin-top: 20px;
}

.stat-card {
    border-radius: 20px;
    padding: 32px;
    width: 280px;
    transition: transform 0.3s ease;
}

.stat-card:hover {
    transform: translateY(-8px);
}

.stat-card-purple {
    background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
    box-shadow: 0 10px 40px rgba(102, 126, 234, 0.3);
}

.stat-card-pink {
    background: linear-gradient(135deg, #f093fb 0%, #f5576c 100%);
    box-shadow: 0 10px 40px rgba(245, 87, 108, 0.3);
}

.stat-card-blue {
    background: linear-gradient(135deg, #4facfe 0%, #00f2fe 100%);
    box-shadow: 0 10px 40px rgba(79, 172, 254, 0.3);
}

.stat-card-icon {
    background: rgba(255,255,255,0.2);
    border-radius: 16px;
    width: 64px;
    height: 64px;
    display: flex;
    align-items: center;
    justify-content: center;
    margin-bottom: 20px;
}

.stat-card-value {
    color: white;
    font-size: 2.5rem;
    font-weight: 800;
    margin-bottom: 4px;
}

.stat-card-label {
    color: rgba(255,255,255,0.8);
    font-size: 1rem;
}

/* Leaderboard Page */
.leaderboard-container {
    background: white;
    border-radius: 20px;
    overflow: hidden;
    box-shadow: 0 10px 40px rgba(0,0,0,0.1);
}

.leaderboard-row {
    display: flex;
    align-items: center;
    padding: 20px 28px;
    border-bottom: 1px solid #f0f0f0;
    transition: background-color 0.2s ease;
}

.leaderboard-row:hover {
    background-color: rgba(102, 126, 234, 0.05);
}

.leaderboard-row-top3 {
    background: linear-gradient(90deg, rgba(102, 126, 234, 0.05) 0%, transparent 100%);
}

.rank-badge {
    width: 48px;
    height: 48px;
    border-radius: 12px;
    display: flex;
    align-items: center;
    justify-content: center;
    margin-right: 20px;
}

.rank-gold { background: linear-gradient(135deg, #FFD700 0%, #FFA500 100%); box-shadow: 0 4px 12px rgba(0,0,0,0.15); }
.rank-silver { background: linear-gradient(135deg, #C0C0C0 0%, #A8A8A8 100%); box-shadow: 0 4px 12px rgba(0,0,0,0.15); }
.rank-bronze { background: linear-gradient(135deg, #CD7F32 0%, #B8860B 100%); box-shadow: 0 4px 12px rgba(0,0,0,0.15); }
.rank-default { background: linear-gradient(135deg, #e8ecf3 0%, #dfe4ed 100%); box-shadow: 0 4px 12px rgba(0,0,0,0.05); }

.rank-number { font-weight: 800; font-size: 1.2rem; }
.rank-number-top3 { color: white; }
.rank-number-default { color: #667eea; }

.player-info { flex: 1; }
.player-name { font-weight: 600; color: #1a1a2e; font-size: 1.1rem; }
.player-stats { margin-top: 4px; display: flex; gap: 16px; }
.player-stat { color: #666; font-size: 0.85rem; }
.player-medal { font-size: 2rem; }

.empty-state {
    background: white;
    border-radius: 16px;
    padding: 48px;
    text-align: center;
    box-shadow: 0 4px 20px rgba(0,0,0,0.08);
}

.empty-state-text { color: #666; }

/* Profile Page */
.profile-card { max-width: 600px; }
.profile-email { color: var(--neutral-foreground-hint); }
.profile-stats { text-align: center; }
.profile-stat-value { color: var(--accent-fill-rest); }
.badges-section-title { margin: 30px 0 10px 0; }
.badge-item { padding: 8px 16px; }
.badge-icon { margin-right: 5px; }
.no-badges-text { color: var(--neutral-foreground-hint); }
.auth-warning-icon { margin-right: 8px; }
```

### 6.2 Modification de MainLayout.razor

Remplacez le contenu par un layout HTML personnalisé (les composants FluentNavLink ne permettent pas un style custom sur fond sombre) :

```razor
@inherits LayoutComponentBase
@using Microsoft.AspNetCore.Components.Authorization
@inject NavigationManager Navigation

<div class="app-layout">
    <header class="app-header">
        <div class="header-brand">
            <div class="brand-icon">
                <FluentIcon Value="@(new Icons.Filled.Size28.Trophy())" Color="@Color.Custom" CustomColor="#FFD700" />
            </div>
            <span class="brand-title">LevelUp</span>
            <span class="brand-badge">BETA</span>
        </div>
        
        <AuthorizeView>
            <Authorized>
                <div class="header-user">
                    <div class="user-avatar">
                        <FluentIcon Value="@(new Icons.Filled.Size20.Person())" Color="@Color.Custom" CustomColor="white" />
                    </div>
                    <span class="user-name">@context.User.Identity?.Name</span>
                    <button @onclick="BeginLogout" class="header-btn logout-btn">
                        <FluentIcon Value="@(new Icons.Regular.Size16.ArrowExit())" Color="@Color.Custom" CustomColor="white" />
                        <span>Déconnexion</span>
                    </button>
                </div>
            </Authorized>
            <NotAuthorized>
                <button @onclick="BeginLogin" class="header-btn login-btn">
                    <FluentIcon Value="@(new Icons.Regular.Size16.PersonAdd())" Color="@Color.Custom" CustomColor="#667eea" />
                    <span>Se connecter</span>
                </button>
            </NotAuthorized>
        </AuthorizeView>
    </header>

    <div class="app-body">
        <nav class="sidebar">
            <div class="sidebar-section-title">Navigation</div>
            
            <NavLink href="/" class="nav-item" Match="NavLinkMatch.All">
                <FluentIcon Value="@(new Icons.Filled.Size20.Home())" Color="@Color.Custom" CustomColor="white" />
                <span>Accueil</span>
            </NavLink>
            
            <NavLink href="/leaderboard" class="nav-item">
                <FluentIcon Value="@(new Icons.Filled.Size20.Trophy())" Color="@Color.Custom" CustomColor="white" />
                <span>Classement</span>
            </NavLink>
            
            <AuthorizeView>
                <Authorized>
                    <NavLink href="/profile" class="nav-item">
                        <FluentIcon Value="@(new Icons.Filled.Size20.Person())" Color="@Color.Custom" CustomColor="white" />
                        <span>Mon Profil</span>
                    </NavLink>
                </Authorized>
            </AuthorizeView>
            
            <AuthorizeView Roles="app_admin">
                <Authorized>
                    <div class="sidebar-section-title sidebar-admin-separator">Administration</div>
                    <NavLink href="/admin" class="nav-item">
                        <FluentIcon Value="@(new Icons.Filled.Size20.Settings())" Color="@Color.Custom" CustomColor="white" />
                        <span>Gestion</span>
                    </NavLink>
                </Authorized>
            </AuthorizeView>
        </nav>

        <main class="main-content">
            @Body
        </main>
    </div>
</div>

<div id="blazor-error-ui">
    Une erreur s'est produite.
    <a href="." class="reload">Recharger</a>
    <span class="dismiss">X</span>
</div>

@code {
    private void BeginLogin()
    {
        Navigation.NavigateToLogin("authentication/login");
    }
    
    private void BeginLogout()
    {
        Navigation.NavigateToLogout("authentication/logout");
    }
}
```

> **Note :** On utilise `NavLink` (composant Blazor natif) au lieu de `FluentNavLink` car ce dernier ne permet pas de personnaliser les couleurs de texte sur un fond sombre.

---

## Étape 7 : Consommer le vrai Leaderboard

### 7.1 Modification de Leaderboard.razor

Remplacez le contenu de `Pages/Leaderboard.razor` :

```razor
@page "/leaderboard"
@inject HttpClient Http

<PageTitle>Classement - LevelUp</PageTitle>

<div class="page-header">
    <div class="page-header-row">
        <div class="page-header-icon">
            <FluentIcon Value="@(new Icons.Filled.Size24.Trophy())" Color="@Color.Custom" CustomColor="white" />
        </div>
        <div>
            <FluentLabel Typo="Typography.H2" Class="page-title">Classement Mondial</FluentLabel>
            <FluentLabel Class="page-subtitle">Les meilleurs joueurs de la plateforme</FluentLabel>
        </div>
    </div>
</div>

@if (!string.IsNullOrEmpty(_errorMessage))
{
    <FluentMessageBar Intent="MessageIntent.Error">
        <FluentIcon Value="@(new Icons.Regular.Size20.ErrorCircle())" Slot="start" />
        @_errorMessage
    </FluentMessageBar>
}

@if (_loading)
{
    <div class="loading-container">
        <FluentProgressRing />
        <FluentLabel Class="loading-text">Chargement du classement...</FluentLabel>
    </div>
}
else if (_leaderboard != null && _leaderboard.Any())
{
    <div class="leaderboard-container">
        @foreach (var player in _leaderboard)
        {
            var isTop3 = player.Rank <= 3;
            var rankClass = player.Rank == 1 ? "rank-gold" : 
                           player.Rank == 2 ? "rank-silver" : 
                           player.Rank == 3 ? "rank-bronze" : "rank-default";
            var rowClass = isTop3 ? "leaderboard-row leaderboard-row-top3" : "leaderboard-row";
            var numberClass = isTop3 ? "rank-number rank-number-top3" : "rank-number rank-number-default";
            
            <div class="@rowClass">
                <div class="rank-badge @rankClass">
                    <span class="@numberClass">@player.Rank</span>
                </div>
                <div class="player-info">
                    <div class="player-name">@player.Name</div>
                    <div class="player-stats">
                        <span class="player-stat">⭐ @player.Xp.ToString("N0") XP</span>
                        <span class="player-stat">🏅 @player.BadgeCount badges</span>
                    </div>
                </div>
                @if (isTop3)
                {
                    var medal = player.Rank == 1 ? "🥇" : player.Rank == 2 ? "🥈" : "🥉";
                    <div class="player-medal">@medal</div>
                }
            </div>
        }
    </div>
}
else if (!_loading)
{
    <div class="empty-state">
        <FluentIcon Value="@(new Icons.Regular.Size48.People())" Color="@Color.Neutral" />
        <FluentLabel Class="empty-state-text">Aucun joueur dans le classement pour le moment.</FluentLabel>
    </div>
}

@code {
    private List<LeaderboardResponse>? _leaderboard;
    private string? _errorMessage;
    private bool _loading = true;

    protected override async Task OnInitializedAsync()
    {
        await LoadLeaderboardAsync();
    }

    private async Task LoadLeaderboardAsync()
    {
        _loading = true;
        _errorMessage = null;
        
        try
        {
            // Appel réel à l'API (le token JWT est injecté automatiquement)
            _leaderboard = await Http.GetFromJsonAsync<List<LeaderboardResponse>>("leaderboard");
        }
        catch (AccessTokenNotAvailableException ex)
        {
            // L'utilisateur n'est pas connecté, redirection vers Keycloak
            ex.Redirect();
        }
        catch (HttpRequestException ex)
        {
            _errorMessage = $"Impossible de contacter l'API. ({ex.Message})";
        }
        finally
        {
            _loading = false;
        }
    }
}
```

---

## Étape 8 : Test de l'intégration

### 8.1 Lancement des services

```bash
# Terminal 1 : Démarrer Docker (Keycloak + SQL Server)
docker compose up -d

# Terminal 2 : Lancer l'API
cd LevelUp.Api
dotnet run

# Terminal 3 : Lancer le Client Blazor
cd LevelUp.Client
dotnet run
```

### 8.2 Vérification

1. Ouvrez **http://localhost:5032** dans votre navigateur
2. Cliquez sur **"Connexion"** dans le header
3. Vous êtes redirigé vers la page de login **Keycloak**
4. Connectez-vous avec `test-user` / `password123`
5. Après redirection, votre nom apparaît dans le header
6. Allez sur la page **Classement** : les vraies données de l'API s'affichent

### 8.3 Vérification du token JWT

Ouvrez les **DevTools** (F12) → Onglet **Network** :
- Cliquez sur une requête vers l'API
- Vérifiez que le header `Authorization: Bearer ey...` est présent

---

## Étape 9 : Connexion de tous les écrans à l'API

### 9.1 Page d'accueil (Home.razor)

La page d'accueil affiche des statistiques globales calculées depuis les données de l'API :

```razor
@page "/"
@inject HttpClient Http

<PageTitle>Accueil - LevelUp</PageTitle>

<div class="page-header">
    <FluentLabel Typo="Typography.H1" Class="home-title">
        Bienvenue sur LevelUp 🎮
    </FluentLabel>
    <FluentLabel Typo="Typography.Body" Class="home-subtitle">
        Suivez votre progression, gagnez des badges et grimpez dans le classement mondial !
    </FluentLabel>
</div>

@if (_loading)
{
    <div class="loading-container">
        <FluentProgressRing />
        <FluentLabel Class="loading-text">Chargement des statistiques...</FluentLabel>
    </div>
}
else if (!string.IsNullOrEmpty(_errorMessage))
{
    <FluentMessageBar Intent="MessageIntent.Warning">
        @_errorMessage
    </FluentMessageBar>
}
else
{
    <FluentStack Orientation="Orientation.Horizontal" Wrap="true" HorizontalGap="24" Class="stats-grid">
        <div class="stat-card stat-card-purple">
            <div class="stat-card-icon">
                <FluentIcon Value="@(new Icons.Filled.Size32.People())" Color="@Color.Custom" CustomColor="white" />
            </div>
            <FluentLabel Typo="Typography.H2" Class="stat-card-value">@_totalPlayers</FluentLabel>
            <FluentLabel Class="stat-card-label">Joueurs inscrits</FluentLabel>
        </div>

        <div class="stat-card stat-card-pink">
            <div class="stat-card-icon">
                <FluentIcon Value="@(new Icons.Filled.Size32.Star())" Color="@Color.Custom" CustomColor="white" />
            </div>
            <FluentLabel Typo="Typography.H2" Class="stat-card-value">@_totalXp.ToString("N0")</FluentLabel>
            <FluentLabel Class="stat-card-label">XP Total distribué</FluentLabel>
        </div>

        <div class="stat-card stat-card-blue">
            <div class="stat-card-icon">
                <FluentIcon Value="@(new Icons.Filled.Size32.Trophy())" Color="@Color.Custom" CustomColor="white" />
            </div>
            <FluentLabel Typo="Typography.H2" Class="stat-card-value">@_totalBadges</FluentLabel>
            <FluentLabel Class="stat-card-label">Badges attribués</FluentLabel>
        </div>
    </FluentStack>
}

@code {
    private int _totalPlayers;
    private int _totalXp;
    private int _totalBadges;
    private bool _loading = true;
    private string? _errorMessage;

    protected override async Task OnInitializedAsync()
    {
        await LoadStatisticsAsync();
    }

    private async Task LoadStatisticsAsync()
    {
        _loading = true;
        _errorMessage = null;
        
        try
        {
            var leaderboard = await Http.GetFromJsonAsync<List<LeaderboardResponse>>("leaderboard");
            
            if (leaderboard != null)
            {
                _totalPlayers = leaderboard.Count;
                _totalXp = leaderboard.Sum(u => u.Xp);
                _totalBadges = leaderboard.Sum(u => u.BadgeCount);
            }
        }
        catch (HttpRequestException)
        {
            _errorMessage = "Impossible de charger les statistiques. L'API est-elle lancée ?";
            _totalPlayers = 0;
            _totalXp = 0;
            _totalBadges = 0;
        }
        finally
        {
            _loading = false;
        }
    }
}
```

### 9.2 Page Profil (Profile.razor)

La page profil affiche les données de l'utilisateur connecté en utilisant l'endpoint `/users/{id}/profile`. Elle est protégée par `<AuthorizeView>` et crée automatiquement l'utilisateur en BDD s'il n'existe pas :

```razor
@page "/profile"
@using Microsoft.AspNetCore.Components.Authorization
@using Microsoft.AspNetCore.Components.WebAssembly.Authentication
@inject HttpClient Http
@inject AuthenticationStateProvider AuthenticationStateProvider
@inject NavigationManager Navigation

<PageTitle>Mon Profil - LevelUp</PageTitle>

<FluentLabel Typo="Typography.H2">Mon Profil</FluentLabel>
<FluentDivider></FluentDivider>

<AuthorizeView>
    <Authorized>
        @if (_loading)
        {
            <div class="loading-container">
                <FluentProgressRing />
                <FluentLabel Class="loading-text">Chargement du profil...</FluentLabel>
            </div>
        }
        else if (!string.IsNullOrEmpty(_errorMessage))
        {
            <FluentMessageBar Intent="MessageIntent.Error">
                @_errorMessage
            </FluentMessageBar>
        }
        else if (_profile != null)
        {
            <FluentCard Class="profile-card">
                <FluentStack Orientation="Orientation.Horizontal" HorizontalGap="20" 
                             VerticalAlignment="VerticalAlignment.Center">
                    <FluentPersona Name="@_profile.Name"
                                   Initials="@GetInitials(_profile.Name)"
                                   ImageSize="64px" />
                    <FluentStack Orientation="Orientation.Vertical">
                        <FluentLabel Typo="Typography.H4">@_profile.Name</FluentLabel>
                        <FluentLabel Typo="Typography.Body" Class="profile-email">
                            @_profile.Email
                        </FluentLabel>
                    </FluentStack>
                </FluentStack>

                <FluentDivider></FluentDivider>

                <FluentStack Orientation="Orientation.Horizontal" Wrap="true" HorizontalGap="40">
                    <FluentStack Orientation="Orientation.Vertical" Class="profile-stats">
                        <FluentLabel Typo="Typography.H3" Class="profile-stat-value">@_profile.TotalXP</FluentLabel>
                        <FluentLabel Typo="Typography.Body">Experience (XP)</FluentLabel>
                    </FluentStack>

                    <FluentStack Orientation="Orientation.Vertical" Class="profile-stats">
                        <FluentLabel Typo="Typography.H3" Class="profile-stat-value">@_profile.Level</FluentLabel>
                        <FluentLabel Typo="Typography.Body">Niveau</FluentLabel>
                    </FluentStack>

                    <FluentStack Orientation="Orientation.Vertical" Class="profile-stats">
                        <FluentLabel Typo="Typography.H3" Class="profile-stat-value">@_profile.Badges.Count</FluentLabel>
                        <FluentLabel Typo="Typography.Body">Badges</FluentLabel>
                    </FluentStack>
                </FluentStack>
            </FluentCard>

            <FluentLabel Typo="Typography.H4" Class="badges-section-title">Mes Badges</FluentLabel>

            @if (_profile.Badges.Any())
            {
                <FluentStack Orientation="Orientation.Horizontal" Wrap="true" HorizontalGap="10">
                    @foreach (var badgeName in _profile.Badges)
                    {
                        <FluentBadge Appearance="Appearance.Accent" Class="badge-item">
                            <FluentIcon Value="@(new Icons.Regular.Size16.Star())" Class="badge-icon" />
                            @badgeName
                        </FluentBadge>
                    }
                </FluentStack>
            }
            else
            {
                <FluentLabel Typo="Typography.Body" Class="no-badges-text">
                    Vous n'avez pas encore de badges. Participez à des activités pour en gagner !
                </FluentLabel>
            }
        }
    </Authorized>
    <NotAuthorized>
        <FluentMessageBar Intent="MessageIntent.Warning">
            <FluentIcon Value="@(new Icons.Regular.Size20.LockClosed())" Class="auth-warning-icon" />
            Vous devez être connecté pour voir votre profil.
        </FluentMessageBar>
    </NotAuthorized>
</AuthorizeView>

@code {
    private UserProfileResponse? _profile;
    private bool _loading = true;
    private string? _errorMessage;

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        if (authState.User.Identity?.IsAuthenticated == true)
        {
            await LoadProfileAsync(authState.User.Identity.Name);
        }
        else
        {
            _loading = false;
        }
    }

    private async Task LoadProfileAsync(string? userName)
    {
        _loading = true;
        _errorMessage = null;

        try
        {
            // Recuperer tous les utilisateurs pour trouver celui connecte
            var users = await Http.GetFromJsonAsync<List<UserResponse>>("users");
            
            if (users != null)
            {
                // Chercher par email ou nom (le userName de Keycloak est souvent l'email)
                var currentUser = users.FirstOrDefault(u => 
                    u.Email.Equals(userName, StringComparison.OrdinalIgnoreCase) ||
                    u.Name.Equals(userName, StringComparison.OrdinalIgnoreCase));

                if (currentUser == null)
                {
                    // L'utilisateur Keycloak n'existe pas en BDD -> le creer automatiquement
                    var newUser = new CreateUserRequest(userName ?? "Utilisateur", userName ?? "user@levelup.dev");
                    var response = await Http.PostAsJsonAsync("users", newUser);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        currentUser = await response.Content.ReadFromJsonAsync<UserResponse>();
                    }
                    else
                    {
                        _errorMessage = "Impossible de creer votre profil automatiquement.";
                        return;
                    }
                }

                if (currentUser != null)
                {
                    // Charger le profil complet via l'endpoint dedié
                    _profile = await Http.GetFromJsonAsync<UserProfileResponse>(
                        $"users/{currentUser.Id}/profile");
                }
            }
        }
        catch (AccessTokenNotAvailableException ex)
        {
            // Le token n'est pas disponible - rediriger vers login
            ex.Redirect();
        }
        catch (HttpRequestException ex)
        {
            _errorMessage = $"Erreur API: {ex.Message}";
        }
        catch (Exception ex)
        {
            _errorMessage = $"Erreur: {ex.Message}";
        }
        finally
        {
            _loading = false;
        }
    }

    private string GetInitials(string name)
    {
        var parts = name.Split(' ');
        if (parts.Length >= 2)
            return $"{parts[0][0]}{parts[1][0]}";
        return name.Length >= 2 ? name[..2].ToUpper() : name.ToUpper();
    }
}
```

> **Points clés :**
> - On utilise `UserProfileResponse` de `LevelUp.Shared.Dtos`
> - Si l'utilisateur Keycloak n'existe pas en BDD, il est **créé automatiquement**
> - `AccessTokenNotAvailableException` redirige vers la page de login si le token expire

### 9.3 Gestion de la visibilité des menus

Dans le `MainLayout.razor`, utilisez `<AuthorizeView>` pour masquer les elements selon l'etat de connexion :

```razor
<FluentNavMenu Width="250" Style="background: var(--neutral-layer-3);">
    @* Menu public *@
    <FluentNavLink Href="/" Match="NavLinkMatch.All" Icon="@(new Icons.Regular.Size20.Home())">
        Accueil
    </FluentNavLink>
    <FluentNavLink Href="/leaderboard" Icon="@(new Icons.Regular.Size20.Trophy())">
        Classement
    </FluentNavLink>
    
    @* Menu visible uniquement pour les utilisateurs connectes *@
    <AuthorizeView>
        <Authorized>
            <FluentNavLink Href="/profile" Icon="@(new Icons.Regular.Size20.Person())">
                Mon Profil
            </FluentNavLink>
        </Authorized>
    </AuthorizeView>
    
    @* Menu admin visible uniquement pour les administrateurs *@
    <AuthorizeView Roles="app_admin">
        <Authorized>
            <FluentNavLink Href="/admin" Icon="@(new Icons.Regular.Size20.Settings())">
                Administration
            </FluentNavLink>
        </Authorized>
    </AuthorizeView>
</FluentNavMenu>
```

> **Patterns AuthorizeView :**
> - `<AuthorizeView>` : Visible pour tout utilisateur connecte
> - `<AuthorizeView Roles="app_admin">` : Visible uniquement pour le role `app_admin`
> - `<NotAuthorized>` : Contenu affiche si l'utilisateur n'est pas autorise

---

## Récapitulatif des fichiers modifiés

| Fichier | Modifications |
|---------|---------------|
| `LevelUp.Api/Program.cs` | Ajout CORS |
| `LevelUp.Client/Program.cs` | HttpClient + OIDC |
| `LevelUp.Client/wwwroot/appsettings.json` | Config Keycloak |
| `LevelUp.Client/_Imports.razor` | Namespaces auth |
| `LevelUp.Client/App.razor` | CascadingAuthenticationState |
| `LevelUp.Client/Shared/RedirectToLogin.razor` | Nouveau fichier |
| `LevelUp.Client/Pages/Authentication.razor` | Nouveau fichier |
| `LevelUp.Client/Layout/MainLayout.razor` | Boutons Login/Logout + menu dynamique |
| `LevelUp.Client/Pages/Leaderboard.razor` | Appel API réel |
| `LevelUp.Client/Pages/Home.razor` | Statistiques depuis l'API |
| `LevelUp.Client/Pages/Profile.razor` | Profil utilisateur depuis l'API |

---

## Dépannage

### Erreur CORS

**Symptôme :** `Access to fetch has been blocked by CORS policy`

**Solution :** Vérifiez que :
1. `UseCors()` est appelé **avant** `UseAuthentication()`
2. Les origines correspondent exactement aux URLs du client Blazor

### Erreur "Unable to obtain access token"

**Symptôme :** Échec de l'authentification, pas de redirection vers Keycloak

**Solution :** 
1. Vérifiez que Keycloak est lancé : `docker compose ps`
2. Vérifiez l'URL dans `appsettings.json` : `http://localhost:8080/realms/LevelUp`
3. Vérifiez que le client `levelup-api` existe dans Keycloak

### Le nom d'utilisateur n'apparaît pas

**Symptôme :** Après connexion, `context.User.Identity?.Name` est null

**Solution :** Dans Keycloak, vérifiez que le Client Scope inclut la claim `preferred_username`

---

## Challenge Final

**Mission : Sécurité visuelle avancée**

1. **Bouton Admin :** Sur la page Leaderboard, ajoutez un bouton "Attribuer un Badge" visible uniquement pour les `app_admin`

2. **Page protégée :** Créez une page `/admin` accessible uniquement aux administrateurs avec l'attribut `[Authorize(Roles = "app_admin")]`

3. **Affichage des rôles :** Dans le header, affichez un badge "Admin" si l'utilisateur a le rôle `app_admin`

---

## Vérification de fin de TP

- [ ] Le projet Blazor démarre et affiche le Layout Fluent UI
- [ ] Le bouton "Connexion" redirige vers Keycloak
- [ ] Après connexion, le nom de l'utilisateur s'affiche dans le header
- [ ] La page **Accueil** affiche les statistiques depuis l'API
- [ ] La page **Classement** affiche les vraies données de l'API
- [ ] La page **Mon Profil** affiche le profil de l'utilisateur connecté
- [ ] Le menu "Mon Profil" n'est visible qu'une fois connecté
- [ ] Le menu "Administration" n'est visible que pour les administrateurs
- [ ] Les appels réseau (F12) montrent le header `Authorization: Bearer ey...`

---

**Félicitations !**

Vous avez maintenant une architecture logicielle complète :
- Base de données SQL Server
- API sécurisée avec JWT
- Serveur d'identité Keycloak
- Front-End moderne Blazor avec Fluent UI
- Authentification OIDC end-to-end