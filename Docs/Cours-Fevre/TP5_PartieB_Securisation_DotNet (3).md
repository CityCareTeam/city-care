# TP 5 - Partie B : Securiser l'API .NET avec JWT et Keycloak

Module : M-4EADL-301 - Developpement Avance et Extreme Programming  
Seance : 5 / 8
Focus : Middleware JWT, Authentification Bearer, Protection des routes.

---

## Objectif de la Partie B

Maintenant que Keycloak est pret (Partie A), nous allons apprendre a notre API .NET a :

1. **Valider** les jetons JWT emis par Keycloak.
2. **Identifier** l'utilisateur qui appelle l'API.
3. **Refuser** l'acces aux personnes non authentifiees.

---

## Etape 1 : Installation du Package JWT

Pour gerer les jetons de securite, .NET a besoin d'un middleware specifique.

1. Ouvrez un terminal dans votre projet **LevelUp.Api**.
2. Installez le package suivant :

```bash
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
```

> **Note :** La version installee sera automatiquement alignee avec votre version de .NET (ex: 10.0.x pour .NET 10).

---

## Etape 2 : Configuration des Services (Program.cs)

Nous devons dire a .NET de faire confiance a notre serveur Keycloak.

1. Ouvrez `Program.cs`.
2. Ajoutez les usings en haut du fichier :

```csharp
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
```

3. Ajoutez la configuration de l'authentification **avant** `builder.Build()` :

```csharp
// --- CONFIGURATION SECURITE ---
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // L'URL de votre Realm Keycloak (vu en Partie A)
        options.Authority = "http://localhost:8080/realms/LevelUp";
        
        // Audience attendue dans le token
        options.Audience = "account";
        
        // Indispensable en local car Keycloak est en HTTP dans Docker
        options.RequireHttpsMetadata = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            // Keycloak peut inclure plusieurs audiences dans le token
            ValidAudiences = new[] { "account", "levelup-api" }
        };
    });

builder.Services.AddAuthorization();
// ------------------------------
```

> **Pourquoi `account` comme Audience ?**  
> Par defaut, Keycloak inclut `account` dans la claim `aud` des tokens. Si vous souhaitez utiliser uniquement `levelup-api`, il faut configurer un "Audience Mapper" dans Keycloak (voir section Troubleshooting).

---

## Etape 3 : Activation des Middlewares

L'ordre est **critique**. Si vous inversez ces lignes, la securite ne fonctionnera pas ou bloquera tout le monde.

1. Reperez l'endroit ou vous utilisez les middlewares (`app.Use...`).
2. Ajoutez `UseAuthentication` et `UseAuthorization` **apres** UseRouting (si present) et **avant** de mapper vos endpoints.

```csharp
var app = builder.Build();

// ... autres middlewares (Swagger, etc.)

app.UseAuthentication(); // "Qui es-tu ?"
app.UseAuthorization();  // "As-tu le droit ?"

app.MapUserEndpoints();
app.MapBadgeEndpoints();

app.Run();
```

---

## Etape 4 : Verrouiller les Endpoints

Par defaut, meme avec le middleware, tout reste ouvert. Il faut explicitement demander la protection.

### 4.1 Proteger les endpoints Users

1. Ouvrez `Endpoints/UserEndpoints.cs`.
2. Ajoutez `.RequireAuthorization()` au groupe :

```csharp
public static void MapUserEndpoints(this WebApplication app)
{
    var group = app.MapGroup("/users")
                   .RequireAuthorization(); // <--- TOUT le groupe est maintenant securise

    group.MapGet("/", GetAllUsers);
    group.MapGet("/{id:int}", GetUserById);
    group.MapPost("/", CreateUser);
    group.MapPut("/{id:int}", UpdateUser);
    group.MapDelete("/{id:int}", DeleteUser);
    
    // Route publique en dehors du groupe protege
    app.MapGet("/leaderboard", GetLeaderboard)
       .AllowAnonymous();
}
```

> **Astuce :** Utilisez `.AllowAnonymous()` pour les routes qui doivent rester publiques (ex: leaderboard, inscription).

### 4.2 Proteger les endpoints Badges

1. Ouvrez `Endpoints/BadgeEndpoints.cs`.
2. Ajoutez `.RequireAuthorization()` au groupe :

```csharp
public static void MapBadgeEndpoints(this WebApplication app)
{
    var group = app.MapGroup("/badges")
                   .RequireAuthorization(); // <--- Groupe securise

    group.MapGet("/", GetAllBadges);
    group.MapGet("/{id:int}", GetBadgeById);
    group.MapPost("/", CreateBadge);
}
```

---

## Etape 5 : Test de la barriere (Echec attendu)

1. Relancez votre API :

```bash
cd LevelUp.Api
dotnet run
```

2. Ouvrez votre fichier `LevelUp.Api.http` et ajoutez la requete suivante :

```http
### U-1: Lister les utilisateurs (SANS TOKEN - doit retourner 401)
GET http://localhost:5207/users
```

3. Cliquez sur **Send Request** au-dessus de la requete.

4. **Resultat attendu :** Vous devriez recevoir une erreur **401 Unauthorized**.

C'est une victoire : votre API est protegee !

---

## Etape 6 : Test avec Token (Succes attendu)

Pour passer la barriere, il faut presenter le "pass" (le Token).

### 6.1 Preparer le fichier .http

Completez votre fichier `LevelUp.Api.http` avec les variables et requetes suivantes :

```http
@baseUrl = http://localhost:5207
@keycloakUrl = http://localhost:8080
@realm = LevelUp
@clientId = levelup-api
@clientSecret = VOTRE_CLIENT_SECRET_ICI

### KC-1: Obtenir un token pour test-admin
# @name getAdminToken
POST {{keycloakUrl}}/realms/{{realm}}/protocol/openid-connect/token
Content-Type: application/x-www-form-urlencoded

grant_type=password&client_id={{clientId}}&client_secret={{clientSecret}}&username=test-admin&password=password

### U-2: Lister les utilisateurs (AVEC TOKEN)
GET {{baseUrl}}/users
Authorization: Bearer {{getAdminToken.response.body.access_token}}
```

### 6.2 Recuperer le Client Secret dans Keycloak

1. Ouvrez Keycloak : http://localhost:8080
2. Connectez-vous avec `admin` / `admin`
3. Selectionnez le Realm **LevelUp** (menu deroulant en haut a gauche)
4. Allez dans **Clients** > **levelup-api**
5. Onglet **Credentials**
6. Copiez le **Client secret** et collez-le dans la variable `@clientSecret` du fichier .http

### 6.3 Tester l'authentification

1. **Executez d'abord** la requete **KC-1** pour obtenir un token (cliquez sur "Send Request").
2. **Executez ensuite** la requete **U-2** qui utilisera automatiquement le token obtenu.

**Resultat attendu :** Retour **200 OK** avec vos donnees.

---

## Fichier LevelUp.Api.http complet

Voici le fichier `.http` complet pour tester toutes les fonctionnalites :

```http
@baseUrl = http://localhost:5207
@keycloakUrl = http://localhost:8080
@realm = LevelUp
@clientId = levelup-api
@clientSecret = VOTRE_CLIENT_SECRET_ICI

### ====================================
### KEYCLOAK - Obtention de tokens
### ====================================

### KC-1: Token pour test-admin (role app_admin)
# @name getAdminToken
POST {{keycloakUrl}}/realms/{{realm}}/protocol/openid-connect/token
Content-Type: application/x-www-form-urlencoded

grant_type=password&client_id={{clientId}}&client_secret={{clientSecret}}&username=test-admin&password=password

### KC-2: Token pour test-user (role app_user)
# @name getUserToken
POST {{keycloakUrl}}/realms/{{realm}}/protocol/openid-connect/token
Content-Type: application/x-www-form-urlencoded

grant_type=password&client_id={{clientId}}&client_secret={{clientSecret}}&username=test-user&password=password

### ====================================
### USERS ENDPOINTS (Proteges)
### ====================================

### U-1: Lister les utilisateurs (SANS TOKEN - doit retourner 401)
GET {{baseUrl}}/users

### U-2: Lister les utilisateurs (avec token admin)
GET {{baseUrl}}/users
Authorization: Bearer {{getAdminToken.response.body.access_token}}

### U-3: Lister les utilisateurs (avec token user)
GET {{baseUrl}}/users
Authorization: Bearer {{getUserToken.response.body.access_token}}

### ====================================
### LEADERBOARD (Public - AllowAnonymous)
### ====================================

### L-1: Leaderboard (sans authentification - doit retourner 200)
GET {{baseUrl}}/leaderboard

### ====================================
### BADGES ENDPOINTS (Proteges)
### ====================================

### B-1: Lister les badges (SANS TOKEN - doit retourner 401)
GET {{baseUrl}}/badges

### B-2: Lister tous les badges (avec token)
GET {{baseUrl}}/badges
Authorization: Bearer {{getAdminToken.response.body.access_token}}
```

> **Important :** Executez toujours **KC-1** ou **KC-2** en premier pour obtenir un token avant d'executer les autres requetes.

---

## Resume de ce qu'il vient de se passer

```
┌─────────────┐     1. Envoi Token      ┌─────────────┐
│   Client    │ ───────────────────────>│   API .NET  │
│  (Postman)  │   Authorization: Bearer │             │
└─────────────┘                         └──────┬──────┘
                                               │
                                    2. Verification Token
                                               │
                                               v
                                        ┌─────────────┐
                                        │  Keycloak   │
                                        │  (Issuer)   │
                                        └──────┬──────┘
                                               │
                                    3. Token valide ?
                                               │
                                               v
                                        ┌─────────────┐
                                        │ 200 OK ou   │
                                        │ 401 Unauth  │
                                        └─────────────┘
```

1. **Le Client** envoie le Token dans le Header `Authorization`.
2. **L'API** intercepte le Token et contacte Keycloak pour verifier la signature.
3. **L'API** autorise ou refuse l'acces selon la validite du token.

---

## Troubleshooting

### Erreur "Audience validation failed"

Si vous obtenez cette erreur, c'est que l'audience du token ne correspond pas a celle attendue.

**Solution 1 :** Ajoutez `account` dans ValidAudiences (comme dans notre configuration).

**Solution 2 :** Configurez un Audience Mapper dans Keycloak :
1. Allez dans **Clients > levelup-api > Client scopes**
2. Cliquez sur **levelup-api-dedicated**
3. Cliquez sur **Add mapper > By configuration > Audience**
4. Configurez :
   - Name : `levelup-api-audience`
   - Included Client Audience : `levelup-api`
   - Add to access token : ON

### Erreur "IDX20803: Unable to obtain configuration"

L'API n'arrive pas a contacter Keycloak.

**Verifications :**
1. Keycloak est-il demarre ? Verifiez dans Docker Desktop ou avec `docker ps` dans le terminal.
2. L'URL est-elle correcte ? Ouvrez http://localhost:8080/realms/LevelUp/.well-known/openid-configuration dans votre navigateur.

### Token expire

Les tokens Keycloak expirent par defaut apres 5 minutes. Regenerez un nouveau token.

---

## Validation finale

| Test | Requete .http | Resultat attendu |
|------|---------------|------------------|
| Sans token | U-1 | 401 Unauthorized |
| Avec token | U-2 | 200 OK |
| Route publique | L-1 | 200 OK |
| Badges sans token | B-1 | 401 Unauthorized |

---

**Prochaine etape :** Maintenant que tout le monde est identifie, nous allons distinguer les **Admins** des **Users** avec l'autorisation basee sur les roles (Partie C).
