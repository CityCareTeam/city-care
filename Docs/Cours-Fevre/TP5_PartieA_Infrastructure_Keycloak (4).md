# **TP 5 \- Partie A : Infrastructure de Sécurité avec Keycloak**

Module : M-4EADL-301 \- Développement Avancé & Extreme Programming  
Séance : 5 / 8  
Focus : Docker Compose, IAM (Identity & Access Management), Configuration Realm & Clients.

## **Objectif de la Partie A**

Dans cette première partie, nous allons déployer notre propre serveur d'identité (**Keycloak**) et le configurer pour qu'il devienne le garant de la sécurité de notre écosystème **LevelUp**.

Nous allons abandonner l'idée de stocker des mots de passe en base de données pour déléguer cette responsabilité à un outil industriel (IAM).

---

## **Problemes Frequents & Solutions**

Avant de commencer, voici les problèmes que vous pourriez rencontrer :

| Problème | Symptôme | Solution |
|----------|----------|----------|
| **Port 8080 déjà utilisé** | Erreur "address already in use" au lancement de Docker | Arrêtez l'application qui utilise le port (souvent un autre serveur Java ou une instance précédente de Keycloak). Sur Mac : `lsof -i :8080` pour identifier le processus. |
| **Conteneur existant** | Erreur "container name already in use" | Supprimez l'ancien conteneur via Docker Desktop ou renommez-le dans le docker-compose.yml |
| **Keycloak pas encore prêt** | Page blanche ou erreur 503 | Keycloak met **15-30 secondes** à démarrer. Attendez et rafraîchissez ! |
| **Erreur "Account not fully set up"** | Impossible d'obtenir un token | L'utilisateur n'a pas de **firstName/lastName** renseigné. Ajoutez-les dans l'onglet Details de l'utilisateur. |
| **Mac Apple Silicon (M1/M2/M3)** | SQL Server ne démarre pas | Ajoutez `platform: linux/amd64` sous l'image SQL Server dans le docker-compose.yml |

---

## **Étape 1 : Création de la Stack Docker (Orchestration)**

Puisque nous avons besoin de SQL Server et de Keycloak simultanément, nous allons créer un fichier docker-compose.yml. Cela permet de lancer toute l'infrastructure d'un coup.

1. À la racine de votre solution (au même niveau que le fichier .sln), créez un fichier nommé docker-compose.yml.  
2. Collez le contenu suivant :

```yaml
services:
  # Notre base de données (configurée au TP 2)
  sql-server:
    image: mcr.microsoft.com/mssql/server:2022-latest
    container_name: levelup-sql
    platform: linux/amd64  # IMPORTANT pour Mac M1/M2/M3
    environment:
      - ACCEPT_EULA=Y
      - MSSQL_SA_PASSWORD=YourStrong@Password
    ports:
      - "1433:1433"

  # Notre nouveau serveur d'identité
  keycloak:
    image: quay.io/keycloak/keycloak:latest
    container_name: levelup-keycloak
    command: start-dev
    environment:
      KC_BOOTSTRAP_ADMIN_USERNAME: admin
      KC_BOOTSTRAP_ADMIN_PASSWORD: admin
    ports:
      - "8080:8080"
    depends_on:
      - sql-server
```

3. Ouvrez un terminal à la racine du projet et lancez la stack :  
   ```
   docker compose up -d
   ```
4. **Attendez 15-30 secondes** que Keycloak démarre complètement.
5. Vérifiez l'accès à la console Keycloak sur [http://localhost:8080](http://localhost:8080). Connectez-vous avec **admin / admin**.

> **Astuce :** Utilisez `docker compose ps` pour vérifier que les deux conteneurs sont bien "Up".

## **Étape 2 : Création du Realm "LevelUp"**

Le **Realm** est votre univers de sécurité isolé.

1. Dans la console Keycloak, cliquez sur le menu déroulant en haut à gauche (**Master**) et cliquez sur **Create Realm**.  
2. Nom du Realm : **LevelUp** (respectez la casse !).  
3. Cliquez sur **Create**.

**Note :** Toutes les configurations suivantes se feront **uniquement** à l'intérieur de ce Realm "LevelUp".

## **Étape 3 : Configuration du Client API**

Nous devons déclarer notre API .NET comme un "Client" autorisé.

1. Allez dans l'onglet **Clients** → **Create client**.  
2. **Client ID :** levelup-api  
3. **Name :** LevelUp Backend API  
4. Cliquez sur **Next**.  
5. **IMPORTANT :** Activez **Client Authentication** (indispensable pour que l'API puisse valider les secrets).  
6. Vérifiez que **Direct access grants** est bien activé (nécessaire pour le grant_type=password).
7. Cliquez sur **Save**.  
8. Dans l'onglet **Settings** du client :  
   * **Valid Redirect URIs :** `http://localhost:5207/*` (l'URL de votre API).  
   * **Web Origins :** `*` (pour autoriser le CORS).  
9. Allez dans l'onglet **Credentials** et **notez le Client Secret**. C'est le mot de passe de votre API pour communiquer avec Keycloak.

> **Notez votre Client Secret ici :** ____________________________

## **Étape 4 : Définition des Rôles (RBAC)**

1. Allez dans **Realm Roles** → **Create role**.  
2. Créez le rôle : **app_admin** (Description : "Administrateur de l'application").  
3. Créez un deuxième rôle : **app_user** (Description : "Utilisateur standard").

## **Étape 5 : Création des Utilisateurs de Test**

### Utilisateur Admin

1. Allez dans **Users** → **Add user**.  
2. Remplissez les champs :
   * **Username :** test-admin
   * **Email :** admin@levelup.com
   * **Email verified :** ON
   * **First name :** Test *(obligatoire pour éviter l'erreur "Account not fully set up")*
   * **Last name :** Admin
3. Cliquez sur **Create**.
4. Dans l'onglet **Credentials** :  
   * Cliquez sur **Set password**
   * Mot de passe : **password**
   * **Desactivez "Temporary"** (sinon l'utilisateur devra changer son mot de passe)
   * Cliquez sur **Save** puis **Save password**
5. Dans l'onglet **Role Mapping** :  
   * Cliquez sur **Assign role**
   * Cliquez sur **Filter by realm roles**
   * Cochez **app_admin** et cliquez sur **Assign**

### Utilisateur Standard

6. **Répétez l'opération** pour créer :
   * **Username :** test-user
   * **Email :** user@levelup.com
   * **First name :** Test
   * **Last name :** User
   * **Mot de passe :** password (non temporaire)
   * **Rôle :** app_user

## **Étape 6 : Test avec Postman**

Postman est l'outil idéal pour tester l'obtention de tokens JWT.

### Configuration de Postman

1. Ouvrez **Postman** (téléchargez-le sur [postman.com](https://www.postman.com/downloads/) si nécessaire).
2. Créez une nouvelle **Collection** nommée "LevelUp Keycloak".
3. Créez une nouvelle requête **POST**.

### Requête pour obtenir un token

4. **URL :** 
   ```
   http://localhost:8080/realms/LevelUp/protocol/openid-connect/token
   ```

5. Onglet **Body** :
   * Sélectionnez **x-www-form-urlencoded**
   * Ajoutez les clés/valeurs suivantes :

   | Key | Value |
   |-----|-------|
   | client_id | levelup-api |
   | client_secret | *[Votre secret de l'étape 3]* |
   | username | test-admin |
   | password | password |
   | grant_type | password |

6. Cliquez sur **Send**.

### Résultat attendu

Vous devriez recevoir un JSON avec :
```json
{
  "access_token": "eyJhbGciOiJSUzI1NiIsInR5cCI...",
  "expires_in": 300,
  "refresh_token": "eyJhbGciOiJIUzUxMiIsInR5cCI...",
  "token_type": "Bearer"
}
```

> **Bravo !** Le token commence par `ey` (signature JWT en base64).

### Décoder le token (Bonus)

7. Copiez l'**access_token** et collez-le sur [jwt.io](https://jwt.io).
8. Dans le payload, vous devriez voir :
   ```json
   {
     "realm_access": {
       "roles": ["app_admin", ...]
     },
     "preferred_username": "test-admin",
     "email": "admin@levelup.com"
   }
   ```

### Testez aussi avec test-user

9. Changez `username` en `test-user` et renvoyez la requête.
10. Vérifiez sur jwt.io que le rôle est bien `app_user`.

---

## **Étape 7 : Vérification dans le fichier .http (Alternative)**

Si vous préférez rester dans VS Code, ajoutez ces requêtes à votre fichier `LevelUp.http` :

```http
@keycloak = http://localhost:8080
@clientSecret = VOTRE_SECRET_ICI

### Obtenir un token pour test-admin
POST {{keycloak}}/realms/LevelUp/protocol/openid-connect/token
Content-Type: application/x-www-form-urlencoded

client_id=levelup-api&client_secret={{clientSecret}}&username=test-admin&password=password&grant_type=password
```

---

## **Checklist de validation**

Avant de passer à la Partie B, vérifiez que :

- [ ] Docker Compose fonctionne (`docker compose ps` montre 2 conteneurs "Up")
- [ ] La console Keycloak est accessible sur http://localhost:8080
- [ ] Le Realm "LevelUp" existe
- [ ] Le client "levelup-api" est créé avec Client Authentication activé
- [ ] Vous avez noté le Client Secret
- [ ] Les rôles app_admin et app_user sont créés
- [ ] Les utilisateurs test-admin et test-user existent avec leurs rôles
- [ ] Vous obtenez un token JWT valide dans Postman
- [ ] Le token décodé sur jwt.io contient le bon rôle

---

### **Prochaine étape**

L'infrastructure est prête. Dans la **Partie B**, nous allons configurer le middleware .NET pour valider ces jetons et protéger nos endpoints.