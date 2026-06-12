# TP 5 - Partie C : Autorisation et Roles (RBAC)

Module : M-4EADL-301 - Developpement Avance et Extreme Programming  
Seance : 5 / 8  
Focus : Claims Mapping, Role-Based Access Control, Securisation fine.

---

## Objectif de la Partie C

Dans la partie precedente, nous avons verrouille l'API : il faut etre connecte pour entrer. Mais actuellement, un utilisateur standard (`test-user`) peut faire les memes actions qu'un administrateur (`test-admin`).

**L'objectif est de mettre en place des privileges :**

| Action | app_user | app_admin |
|--------|----------|-----------|
| Lister les utilisateurs | Oui | Oui |
| Lister les badges | Oui | Oui |
| Creer un badge | Non | Oui |
| Attribuer un badge | Non | Oui |

---

## Etape 1 : Le probleme du Mapping des Roles

Par defaut, .NET cherche les roles dans une claim au format Microsoft.
Cependant, Keycloak envoie les roles dans une structure JSON specifique :

```json
{
  "realm_access": {
    "roles": ["app_admin", "offline_access", ...]
  }
}
```

Nous devons :
1. **Configurer Keycloak** pour exposer les roles dans une claim `roles` au premier niveau.
2. **Configurer .NET** pour lire cette claim et la transformer en claims de role.

---

## Etape 2 : Configuration du Mapper dans Keycloak

1. Ouvrez la console Keycloak : http://localhost:8080
2. Connectez-vous avec `admin` / `admin`
3. Selectionnez le Realm **LevelUp**
4. Allez dans **Clients** > **levelup-api**
5. Onglet **Client scopes** > Cliquez sur **levelup-api-dedicated**
6. Cliquez sur **Add mapper** > **By configuration**
7. Selectionnez **User Realm Role**
8. Configurez le mapper :

| Champ | Valeur |
|-------|--------|
| Name | `roles` |
| Token Claim Name | `roles` |
| Add to ID token | ON |
| Add to access token | ON |
| Add to userinfo | ON |

9. Cliquez sur **Save**

> **Verification :** Obtenez un nouveau token et decodez-le sur [jwt.io](https://jwt.io). Vous devriez voir une claim `roles` contenant un tableau : `["app_admin", "offline_access", ...]`

---

## Etape 3 : Configuration du Mapping dans Program.cs

Keycloak envoie les roles dans un tableau JSON `["app_admin", "app_user", ...]`.
.NET s'attend a des claims individuelles de type `role` pour que `RequireRole()` fonctionne.

Nous devons transformer le tableau en claims distinctes via l'evenement `OnTokenValidated`.

1. Ouvrez `Program.cs`
2. Ajoutez les usings necessaires en haut du fichier :

```csharp
using System.Security.Claims;
using System.Text.Json;
```

3. Modifiez la configuration JWT Bearer :

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "http://localhost:8080/realms/LevelUp";
        options.Audience = "account";
        options.RequireHttpsMetadata = false;

        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidAudiences = new[] { "account", "levelup-api" },
            NameClaimType = "preferred_username"
        };

        // Transformation des roles Keycloak en claims .NET
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                // Recuperation de la claim "roles" (tableau JSON)
                var rolesClaim = context.Principal?.FindFirst("roles");
                if (rolesClaim != null && context.Principal?.Identity is ClaimsIdentity identity)
                {
                    // Parsing du tableau et ajout de chaque role comme claim individuelle
                    var roles = JsonSerializer.Deserialize<string[]>(rolesClaim.Value);
                    if (roles != null)
                    {
                        foreach (var role in roles)
                        {
                            identity.AddClaim(new Claim(ClaimTypes.Role, role));
                        }
                    }
                }
                return Task.CompletedTask;
            }
        };
    });
```

---

## Etape 4 : Verrouiller par Role (RBAC)

Maintenant que .NET comprend les roles, nous pouvons restreindre nos endpoints.

### 4.1 Proteger la creation de badges (Admin uniquement)

1. Ouvrez `Endpoints/BadgeEndpoints.cs`
2. Modifiez les routes pour restreindre l'acces :

```csharp
public static void MapBadgeEndpoints(this WebApplication app)
{
    var group = app.MapGroup("/badges")
        .RequireAuthorization();

    // GET /badges - Accessible a tout utilisateur authentifie
    group.MapGet("/", GetAllBadges);

    // POST /badges - ADMIN UNIQUEMENT
    group.MapPost("/", CreateBadge)
        .RequireAuthorization(policy => policy.RequireRole("app_admin"));

    // POST /users/{userId}/badges - ADMIN UNIQUEMENT
    app.MapPost("/users/{userId}/badges", AssignBadgeToUser)
        .RequireAuthorization(policy => policy.RequireRole("app_admin"));
}
```

### 4.2 Les endpoints Users restent accessibles

Dans `UserEndpoints.cs`, la liste des utilisateurs reste accessible a tout utilisateur authentifie :

```csharp
var group = app.MapGroup("/users")
    .RequireAuthorization(); // Tout utilisateur authentifie peut acceder
```

---

## Etape 5 : Tests de Permissions

Ajoutez ces requetes a votre fichier `LevelUp.Api.http` :

```http
### ====================================
### TESTS RBAC - PARTIE C
### ====================================

### RBAC-1: test-user tente de creer un badge (doit: 403 Forbidden)
# Executez d'abord KC-2 pour obtenir un token test-user
POST {{baseUrl}}/badges
Authorization: Bearer {{getUserToken.response.body.access_token}}
Content-Type: application/json

{
  "name": "Badge Interdit",
  "imageUrl": "https://example.com/forbidden.png"
}

### RBAC-2: test-admin cree un badge (doit: 201 Created)
# Executez d'abord KC-1 pour obtenir un token test-admin
POST {{baseUrl}}/badges
Authorization: Bearer {{getAdminToken.response.body.access_token}}
Content-Type: application/json

{
  "name": "Badge Admin",
  "imageUrl": "https://example.com/admin.png"
}

### RBAC-3: test-user liste les badges (doit: 200 OK)
GET {{baseUrl}}/badges
Authorization: Bearer {{getUserToken.response.body.access_token}}

### RBAC-4: test-user liste les utilisateurs (doit: 200 OK)
GET {{baseUrl}}/users
Authorization: Bearer {{getUserToken.response.body.access_token}}
```

### Resultats attendus

| Requete | test-user | test-admin |
|---------|-----------|------------|
| GET /users | 200 OK | 200 OK |
| GET /badges | 200 OK | 200 OK |
| POST /badges | **403 Forbidden** | 201 Created |
| POST /users/{id}/badges | **403 Forbidden** | 201 Created |

---

## Resume : Flux d'autorisation

```
┌─────────────┐     Token JWT        ┌─────────────┐
│   Client    │ ──────────────────>  │   API .NET  │
│             │   roles: [app_user]  │             │
└─────────────┘                      └──────┬──────┘
                                            │
                                   RequireRole("app_admin")
                                            │
                                            v
                                     ┌─────────────┐
                                     │ app_user    │
                                     │ in roles ?  │
                                     └──────┬──────┘
                                            │
                                      Non   │
                                            v
                                     ┌─────────────┐
                                     │ 403         │
                                     │ Forbidden   │
                                     └─────────────┘
```

---

## Troubleshooting

### Le role n'est pas reconnu (403 meme pour admin)

**Cause probable :** Le mapper Keycloak n'est pas configure ou le token n'a pas ete regenere.

**Solution :**
1. Verifiez le mapper dans Keycloak (Etape 2)
2. **Regenerez un nouveau token** (les anciens tokens n'ont pas la claim `roles`)
3. Decodez le token sur [jwt.io](https://jwt.io) et verifiez la presence de `"roles": ["app_admin", ...]`

### La claim "roles" n'apparait pas dans le token

**Cause :** Le mapper n'est pas dans le bon scope.

**Solution :** Assurez-vous d'avoir ajoute le mapper dans **levelup-api-dedicated** (pas dans un autre scope).

### Erreur "Value cannot be null" au demarrage

**Cause :** Le code de transformation des roles echoue si la claim est absente.

**Solution :** Le code utilise deja des verifications null-safe (`?.`). Assurez-vous d'avoir copie le code correctement.

---

## Validation finale

| Test | Action | Resultat attendu |
|------|--------|------------------|
| 1 | Token test-user + GET /users | 200 OK |
| 2 | Token test-user + GET /badges | 200 OK |
| 3 | Token test-user + POST /badges | **403 Forbidden** |
| 4 | Token test-admin + POST /badges | 201 Created |
| 5 | Token test-admin + GET /badges | 200 OK |

---

## Bilan de la Seance 5

| Concept | Ce que nous avons appris |
|---------|--------------------------|
| **Infrastructure** | Keycloak est notre source de verite pour l'identite |
| **Authentification (AuthN)** | Le middleware JWT verifie la signature du jeton |
| **Autorisation (AuthZ)** | Le RBAC permet de restreindre les actions sensibles |
| **Stateless** | L'API ne stocke rien en session, tout est dans le jeton |
| **Claims Mapping** | Transformation des roles Keycloak en claims .NET |

---

## Bonus : Challenge Autonomie

**Mission : Proteger l'ajout d'XP**

Actuellement, n'importe quel utilisateur connecte peut ajouter de l'XP a n'importe qui.

**Objectif :** Modifier la route `POST /users/{id}/activities` pour qu'elle necessite :
- Soit le role `app_admin`
- Soit que l'utilisateur connecte modifie son propre profil

**Indice :** Vous pouvez acceder aux claims de l'utilisateur dans votre handler :

```csharp
private static async Task<IResult> AddActivity(
    int id,
    [FromBody] CreateActivityRequest request,
    IUserXpService xpService,
    ClaimsPrincipal user)  // Injection automatique de l'utilisateur connecte
{
    // Recuperer le username du token
    var currentUsername = ....

    // Verifier si admin
    var isAdmin = .....

    // Votre logique ici...
}
```
