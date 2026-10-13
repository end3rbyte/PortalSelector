# Règles de PortalSelector

## Identité et catalogue des portails

- Le mod fonctionne côté client et conserve les données personnelles des portails dans les données du personnage, séparément pour chaque monde.
- À sa découverte, un portail reçoit un identifiant persistant `Landoria.PortalSelector.Guid` dans son ZDO lorsque le client en est propriétaire. Sans GUID disponible, il n'entre pas dans le catalogue.
- Le GUID identifie le portail, son nom personnel et ses connexions. Le ZDOID sert d'adresse réseau actuelle ; la position ne sert jamais d'identifiant.
- Chaque entrée du catalogue conserve le GUID, le ZDOID, le repère partagé, l'état `active` et des métadonnées comme la position et le biome. Ces métadonnées servent notamment à l'affichage et au tri.
- Le catalogue fondé sur les GUID n'importe pas les listes, noms et connexions enregistrés avant l'utilisation des GUID. Le format GUID précédent reste lisible.
- Un portail qui ne répond pas passe à `active=false` dans le catalogue. Il y reste s'il est lié à un autre portail connu ; sinon, le nettoyage au démarrage le supprime.
- Les données du personnage sont écrites sur disque lors de la sauvegarde normale de Valheim.

## Découverte et disponibilité

- Le client examine les portails connus localement environ toutes les cinq secondes et découvre aussi le partenaire vanilla d'un portail lorsque celui-ci est chargé.
- Dès que Valheim signale la destruction d'un portail connu, le mod passe son entrée à `active=false`, même si son ZDO n'était pas chargé localement.
- Au premier spawn du joueur à chaque connexion, les portails enregistrés sont vérifiés une fois. Les requêtes sont décalées de 250 ms et chaque portail dispose de 10 secondes pour répondre.
- Une réponse correspondant au GUID confirme que l'objet est disponible et rend le portail actif. Une expiration le rend inactif ; elle ne prouve pas sa destruction.
- Lorsqu'une destination change, l'ancienne destination non encore confirmée fait l'objet d'une vérification distincte de trois secondes. Si elle ne répond pas, elle devient inactive.
- Après la vérification du premier spawn, le mod supprime du catalogue chaque portail `active=false` qui n'a de connexion personnelle ni vers un autre portail connu ni depuis un autre portail connu. Son nom, sa route et son marqueur d'import sont aussi effacés. Un portail inactif encore lié dans un sens reste enregistré.
- Un portail inactif encore dans le catalogue peut redevenir actif s'il est redécouvert. Après sa suppression, il entre comme nouveau portail lors d'une nouvelle découverte.

## Noms personnels

- **E** ouvre le dialogue de renommage. Le nom personnel ne modifie pas le repère partagé vanilla.
- Un nom est débarrassé de ses espaces aux extrémités, ne peut pas être vide et est limité à 50 caractères. Le bouton OK est désactivé si le nom est vide.
- Deux portails actifs ne peuvent pas avoir le même nom personnel, sans distinction entre majuscules et minuscules. Si le portail portant ce nom pourrait être inaccessible, son ZDO est vérifié pendant trois secondes au maximum.
- Si le doublon répond encore, le nom est refusé ; le dialogue reste ouvert et le focus revient dans le champ de saisie.
- Un nouveau portail peut reprendre le nom personnel d'un portail inactif, quelle que soit la distance. Il récupère le GUID et les connexions de cette entrée, avec son propre ZDOID et ses nouvelles métadonnées.
- Les noms personnels initiaux proviennent des repères partagés vanilla. Dans une paire connectée, un portail peut recevoir le suffixe `_2` pour distinguer les deux noms.
- Au premier scan sans marqueur d'import, le mod importe ensemble le repère et le partenaire relié par vanilla. Cela couvre aussi les portails connus avant cette version. Si le ZDO ou le GUID du partenaire manque, il attend leur disponibilité avant de terminer l'import.
- Un marqueur lié au GUID termine cet import une seule fois. Les scans suivants ne réimportent ni le repère ni le lien vanilla et ne remplacent pas les noms ou destinations choisis personnellement.

## Destinations et téléportation

- **Maj + E** ouvre le sélecteur de destination uniquement depuis un portail actif et nommé.
- Le sélecteur affiche les autres portails seulement s'ils sont actifs et nommés personnellement. Un portail devenu inactif disparaît d'un sélecteur déjà ouvert et ne peut pas être confirmé.
- Les destinations sont regroupées dans l'ordre des biomes, puis triées dans chaque biome par distance au centre de la carte et enfin par nom.
- Cliquer sur une option change la sélection provisoire ; **OK** l'enregistre. Annuler ou fermer le menu sans confirmer ne modifie pas la connexion enregistrée.
- Une connexion enregistrée relie le GUID du portail source à celui de la destination. Renommer l'un des portails ne change pas cette connexion.
- Une destination enregistrée et connue fait apparaître le portail source comme connecté et allume son feu, même si cette destination est `active=false`. Le texte affiché à proximité montre les noms personnels et l'état de connexion traduit par vanilla.
- Avant la téléportation, le client demande le ZDO de la destination si nécessaire et attend un GUID correspondant. Le voyage ne commence que lorsque cet objet est disponible.
- La téléportation respecte aussi les restrictions de Valheim concernant les paramètres du joueur, les événements de boss et les objets transportés.
- Renommer un portail ou choisir une destination ne modifie ni les repères partagés ni les connexions vanilla.

## Interface et configuration

- Le menu des destinations s'ouvre sous son bouton, a la même largeur et se ferme si l'on clique à l'extérieur, sans changer la sélection.
- La molette fait défiler le menu sans zoomer la caméra tant que le dialogue est ouvert.
- Le portail classique en bois coûte 30 bois précieux, 15 yeux de nain gris et 3 cœurs de surtling (facteur de coût 1,5).
- `Diagnostics.EnableDebugLogs` contrôle les entrées `[Debug]` du fichier BepInEx `LogOutput.log` pour la découverte, les vérifications, les connexions et la téléportation.
