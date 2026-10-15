# Installer Arcanum

1. Téléchargez la distribution Windows du launcher fournie par Keros.
2. Décompressez **toute** l’archive dans un dossier temporaire.
3. Double-cliquez sur `Installer-Arcanum.cmd`. Il copie le launcher dans votre dossier utilisateur et ajoute un raccourci Arcanum au Bureau, sans droits administrateur.
4. Si le dépôt n’a pas été préconfiguré, ouvrez **Paramètres**, puis saisissez le propriétaire et le nom du dépôt communiqués par Keros.
5. Cliquez sur **Installer Arcanum**. Le launcher télécharge, vérifie et installe le jeu.
6. Cliquez sur **Jouer**.

Lors des démarrages suivants, les mises à jour sont installées automatiquement si la case correspondante est cochée. Le bouton **Vérifier** permet aussi de les rechercher et installer manuellement.

Pour une utilisation portable, lancez directement `Launcher/ArcanumLauncher.exe` après extraction. Le jeu et les paramètres restent enregistrés dans le dossier utilisateur.

## Problèmes courants

- **GitHub inaccessible :** réessayez plus tard. Si le jeu est installé, Jouer reste disponible pour le solo. Le launcher ne garantit pas la compatibilité multijoueur d’une ancienne version.
- **Dépôt introuvable :** vérifier son nom, sa visibilité publique et l’existence d’une release stable publiée.
- **Jeu ouvert :** fermez Arcanum avant de cliquer sur Vérifier.
- **Téléchargement interrompu :** cliquez sur Vérifier ou Installer pour recommencer. La V1 ne reprend pas un téléchargement partiel.
- **Dossier non reconnu :** choisissez un nouveau dossier vide. Le launcher ne remplace pas une installation manuelle existante.
- **Erreur SHA-256 :** ne supprimez pas les contrôles ; signalez l’erreur au développeur, qui republiera ZIP et empreinte ensemble.
- **Paramètres corrompus :** launcher fermé, renommez `%LOCALAPPDATA%/Arcanum/Launcher/Configuration/settings.json`, puis relancez et reconfigurez le dépôt et le chemin du jeu.

Les journaux sont accessibles depuis Paramètres. Le binaire produit n’est pas signé : Windows peut afficher un avertissement de provenance. Vérifiez que le fichier vient bien du développeur.

## Mettre à jour le launcher

Fermez le launcher, extrayez la nouvelle distribution officielle puis relancez `Installer-Arcanum.cmd`. Ce remplacement concerne seulement le dossier Launcher. Il conserve `settings.json` ainsi que Game, Game.previous et les sauvegardes Godot. Ne remplacez jamais un launcher pendant qu’il est ouvert.
