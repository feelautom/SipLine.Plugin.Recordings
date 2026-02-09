# Plugin Recordings - Cahier des Charges

## Objectif

Créer un plugin de gestion des enregistrements d'appels avec une interface dédiée permettant de visualiser, lire, exporter et supprimer les fichiers audio enregistrés.

## Emplacement UI

### Accès principal
- **Nouvel onglet dans la sidebar** : "Enregistrements" (3ème onglet après Récents et Contacts)
- **Icône** : Micro ou cassette audio (Lucide: `mic` ou `file-audio`)
- **Comportement** : Clic → affiche la vue Enregistrements en pleine page

### Vue Enregistrements
- Remplace la zone centrale (comme ActiveCall)
- Prend toute la largeur disponible
- Header avec titre + bouton "Ouvrir le dossier"

## Interface Tableau

### Colonnes
| Colonne | Description | Tri |
|---------|-------------|-----|
| Date | Date et heure de l'enregistrement | ✓ (défaut: récent en haut) |
| Contact | Nom du contact ou numéro si inconnu | ✓ |
| Direction | Icône entrant/sortant | - |
| Durée | Durée de l'enregistrement (mm:ss) | ✓ |
| Taille | Taille du fichier (KB/MB) | ✓ |
| Actions | Boutons d'action | - |

### Actions par ligne
| Action | Icône | Description |
|--------|-------|-------------|
| Lire | ▶️ Play | Ouvre un mini-lecteur audio intégré |
| Email | 📧 Mail | Ouvre le client mail avec le fichier en pièce jointe |
| Exporter | 💾 Download | Dialogue "Enregistrer sous" pour copier le fichier |
| Supprimer | 🗑️ Trash | Supprime avec confirmation |

### Lecteur audio intégré
- Barre de progression cliquable
- Bouton Play/Pause
- Affichage temps actuel / durée totale
- Bouton Stop (ferme le lecteur)

## Fonctionnalités avancées

### Filtres
- Recherche par nom/numéro (champ texte)
- Filtre par période (Aujourd'hui, Cette semaine, Ce mois, Tout)

### Actions en lot
- Checkbox de sélection multiple
- "Tout sélectionner" en header
- Actions groupées : Supprimer sélection, Exporter sélection

### Statistiques (header)
- Nombre total d'enregistrements
- Espace disque utilisé

## Données

### Source
- Dossier : `%USERPROFILE%\SipLine\Recordings\`
- Format : MP3 (converti automatiquement depuis WAV)
- Nommage : `{date}_{heure}_{numero}.mp3`

### Métadonnées
- Extraites du nom de fichier
- Enrichies via l'historique d'appels (nom du contact)
- Durée lue depuis les tags MP3 ou calculée

## Intégration Plugin SDK

### Services utilisés
- `IPluginContext.PluginDataPath` - Pour stocker les préférences
- `IPluginContext.ShowSnackbar()` - Notifications (suppression OK, erreur, etc.)
- `IPluginContext.AddLog()` - Logs debug

### Événements
- Pas d'événements SIP nécessaires (lecture seule des fichiers)

### UI
- Le plugin enregistre un **onglet sidebar** (nouveau type à ajouter au SDK si nécessaire)
- Ou utilise `HasSettingsUI = true` avec une vue custom

## Considérations techniques

### Performance
- Chargement lazy des fichiers (pagination si > 100 enregistrements)
- Cache des métadonnées
- Lecture audio via NAudio (déjà intégré)

### Sécurité
- Confirmation avant suppression
- Pas d'accès réseau (tout local)

## Phases d'implémentation

### Phase 1 : Structure de base
- [ ] Créer le projet plugin
- [ ] Ajouter l'onglet sidebar "Enregistrements"
- [ ] Vue basique avec liste des fichiers

### Phase 2 : Tableau et actions
- [ ] DataGrid avec colonnes
- [ ] Action Lire (lecteur intégré)
- [ ] Action Supprimer
- [ ] Action Exporter

### Phase 3 : Fonctionnalités avancées
- [ ] Action Email
- [ ] Filtres et recherche
- [ ] Sélection multiple
- [ ] Statistiques header

### Phase 4 : Polish
- [ ] Tri des colonnes
- [ ] Persistence des préférences (tri, filtres)
- [ ] Gestion des erreurs (fichier manquant, etc.)
