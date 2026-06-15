# Lot 2 — Photos des incidents

Document récapitulatif pour l'équipe (backend CityCare).

---

## Pourquoi

Un signalement urbain (nid de poule, dépôt sauvage, éclairage…) est plus utile **avec une photo**. Le citoyen ou l'agent doit pouvoir :

- **Uploader** une image sur un incident
- **Consulter** les photos liées
- **Supprimer** une photo (auteur ou agent)

Le frontend affichera ces images dans une galerie sur la fiche incident.

---

## Ce qui a été livré

| Méthode | Route | Qui peut l'utiliser | Description |
|---------|-------|---------------------|-------------|
| `POST` | `/incidents/{id}/photos` | Auteur, agent, admin | Upload d'une image |
| `GET` | `/incidents/{id}/photos` | Tout le monde | Liste des photos |
| `DELETE` | `/incidents/{id}/photos/{photoId}` | Auteur, agent, admin | Suppression |

### Upload — détails

- Format : **`multipart/form-data`**, champ **`file`**
- Extensions : `.jpg`, `.jpeg`, `.png`, `.webp`
- Taille max : **5 Mo** (configurable)
- Réponse : JSON avec une **`url`** publique pour afficher l'image

**Exemple de réponse :**
```json
{
  "id": "bf36a340-73ff-4273-97a2-a21487656ae2",
  "incidentId": "c123dd7a-15f0-4795-bfdb-e7ae05f8ce62",
  "url": "http://localhost:9000/citycare-photos/incidents/.../xxx.png",
  "fileName": "steak.png",
  "contentType": "image/png",
  "sizeBytes": 470169,
  "uploadedByUserId": "...",
  "createdAt": "2026-06-11T13:24:24+02:00"
}
```

Le front affiche la photo avec : `<img src="{url}" />`.

---

## La solution technique

On **ne stocke pas l'image en base de données**. On sépare :

| Où | Quoi |
|----|------|
| **MinIO** | Le fichier image (le blob binaire) |
| **PostgreSQL** (`incident_photos`) | Les métadonnées : nom, taille, auteur, chemin MinIO |

```
Frontend ── POST file ──► API .NET ──► MinIO (fichier)
                              │
                              └──► PostgreSQL (métadonnées)

Affichage : GET url ──► MinIO renvoie l'image directement
```

---

## Pourquoi MinIO ?

**MinIO** = stockage de fichiers type « blob », compatible **Amazon S3**, qui tourne en **Docker**.

| | |
|---|---|
| ✅ Pas besoin d'un serveur de fichiers séparé custom | |
| ✅ URLs publiques pour afficher les images | |
| ✅ Même logique qu'AWS S3 / Azure Blob en prod | |
| ✅ Gratuit en local | |

Service ajouté dans `docker-compose.yml` :
- Port **9000** : API stockage (URLs des photos)
- Port **9001** : Console web (`minioadmin` / `minioadmin`)

Bucket utilisé : **`citycare-photos`**

---

## Fichiers ajoutés / modifiés

| Fichier | Rôle |
|---------|------|
| `CityCare.Core/Entities/IncidentPhoto.cs` | Entité métadonnées |
| `CityCare.Api/Services/PhotoStorageService.cs` | Upload / delete / URL MinIO |
| `CityCare.Api/Models/DTOs/Incidents/PhotoResponse.cs` | Format JSON de réponse |
| `CityCare.Api/Controllers/IncidentsController.cs` | 3 endpoints photos |
| `CityCare.Infrastructure/Migrations/...AddIncidentPhotos.cs` | Table `incident_photos` |
| `docker-compose.yml` | Service MinIO + config API |
| `appsettings.json` | Section `Minio` |

---

## Petit lexique

| Terme | Définition |
|-------|------------|
| **Blob / Object storage** | Stockage de fichiers binaires (images, PDF…) — ici MinIO |
| **MinIO** | Serveur S3-compatible open source, en Docker |
| **Bucket** | « Dossier » dans MinIO (`citycare-photos`) |
| **ObjectKey** | Chemin du fichier dans le bucket (`incidents/{id}/{guid}.png`) |
| **multipart/form-data** | Format HTTP pour envoyer un fichier (comme une pièce jointe) |
| **Métadonnées** | Infos sur le fichier (nom, taille, auteur…) — en BDD, pas le fichier lui-même |

---

## Comment tester

```bash
export API=http://localhost:5158

# Login
export TOKEN=$(curl -s -X POST "$API/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"username":"testadmin","password":"test"}' | jq -r '.accessToken')

# Upload (remplacer INCIDENT_ID et le chemin de l'image)
curl -s -X POST "$API/incidents/$INCIDENT_ID/photos" \
  -H "Authorization: Bearer $TOKEN" \
  -F "file=@/Users/.../Downloads/steak.png;type=image/png" | jq .

# Lister
curl -s "$API/incidents/$INCIDENT_ID/photos" | jq .

# Supprimer
curl -X DELETE "$API/incidents/$INCIDENT_ID/photos/$PHOTO_ID" \
  -H "Authorization: Bearer $TOKEN"
```

**Voir les fichiers dans MinIO** : http://localhost:9001

---

## Ce que le frontend doit faire

| Action | Appel |
|--------|-------|
| Afficher la galerie | `GET /incidents/{id}/photos` |
| Envoyer une photo | `POST /incidents/{id}/photos` avec `FormData` + champ `file` |
| Supprimer | `DELETE /incidents/{id}/photos/{photoId}` |

---

## Prod (plus tard)

Remplacer MinIO local par **AWS S3** ou **Azure Blob** via les variables d'environnement `Minio__*` — le code reste le même.

---

*CityCare — Lot 2 Photos — Juin 2026*
