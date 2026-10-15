# Configuration et architecture

## Paramètres

`Launcher/Configuration/defaults.json` accompagne la distribution. Il est lu uniquement tant que le fichier personnel `%LOCALAPPDATA%/Arcanum/Launcher/Configuration/settings.json` n’existe pas.

```json
{
  "Owner": "VOTRE_COMPTE",
  "Repository": "arcanum-releases",
  "AutoCheck": true,
  "Channel": "stable",
  "Language": "fr"
}
```

`GameDirectory` est facultatif dans les valeurs par défaut ; il vaut `%LOCALAPPDATA%/Arcanum/Game`. L’interface enregistre un chemin absolu. `Channel` et `Language` préparent l’évolution du format ; seule la combinaison stable/fr est implémentée, quel que soit leur contenu.

## Composants

- **Arcanum.Core** : modèles, sérialisation atomique, comparaison de versions, API GitHub, téléchargement, extraction et remplacement transactionnel. Aucune dépendance WPF.
- **Arcanum.Launcher** : vues XAML, ViewModel, commandes légères, paramètres et lancement du processus.
- **Arcanum.Tests** : exécutable de tests sans dépendance NuGet de test. HTTP simulé et vrais fichiers ZIP temporaires ; échec => code de sortie non nul.

L’API publique `GET /repos/{owner}/{repo}/releases/latest` sélectionne la publication stable indiquée comme latest par GitHub. Le développeur doit choisir correctement la dernière release. Le launcher compare ensuite les versions numériques `vX.Y.Z`, refuse les préversions et ne rétrograde pas automatiquement.

Les URLs d’assets doivent provenir du dépôt configuré sur github.com, en HTTPS. SHA-256 est requis via le fichier compagnon ; il détecte la corruption mais ne remplace pas la confiance dans le compte GitHub qui publie les deux fichiers.

## Transaction d’installation

1. Verrou exclusif `Game.lock` et vérification des processus Arcanum.
2. Restauration de `Game.previous` si `Game` manque après une interruption.
3. Téléchargement vers `Game.staging/download.zip`.
4. Vérification SHA-256, validation des chemins et contrôle de l’espace disponible.
5. Extraction dans `Game.staging/payload` ; présence obligatoire d’Arcanum.exe à sa racine.
6. Copie des fichiers personnels non gérés. Une collision avec la nouvelle distribution bloque la mise à jour.
7. Écriture de `.arcanum-files.json` et `.arcanum-install.json` **dans la nouvelle installation**.
8. Dernier contrôle d’annulation et du processus ; suppression de la sauvegarde précédente, renommage de Game vers Game.previous, puis du payload vers Game.
9. Si le deuxième renommage échoue, restauration de l’ancienne installation. Si l’ordinateur s’arrête entre les deux, récupération au prochain démarrage.

La phase de renommage ne peut pas être annulée en son milieu. Fermer la fenêtre pendant une opération demande l’annulation mais ne termine pas brutalement le processus. Les métadonnées de version voyagent avec leur installation.

Les fichiers livrés par une release précédente sont considérés comme gérés et remplacés/supprimés à la mise à jour suivante. **Ne stockez pas de données joueur dans un fichier distribué comme ressource du jeu.** Les sauvegardes Godot `user://` dans AppData ne sont jamais touchées. Les répertoires réservés save/saves/profile/profiles/user_data/config/configuration et certains fichiers de paramètres sont refusés dans les ZIP. Les fichiers locaux non gérés sont conservés. Les jonctions et liens de dossiers sont refusés.

Le dossier Game est exclusivement géré par le launcher. La protection des chemins suppose que l’utilisateur local ne remplace pas les dossiers pendant une installation. Les archives et comptes de publication doivent être de confiance.

## Limites délibérées V1

- Windows x64 ; aucune validation de rendu ou d’exécution Windows dans un environnement Linux.
- Pas d’authentification GitHub, dépôt privé, canal bêta, signature Authenticode ou auto-update du launcher. Remplacement manuel séparé fourni.
- Pas de reprise partielle des téléchargements, de patch différentiel, ni de réparation automatique de chaque fichier.
- Limites : ZIP 8 Gio, extraction 24 Gio, 100 000 entrées ; marge disque 256 Mio en plus du volume prévu.
- La détection du jeu par nom de processus est volontairement conservatrice. L’exécutable doit s’appeler Arcanum.exe.
- Le launcher ne lit ni ne modifie les paramètres Playit/ENet. Aucun état de serveur fictif. Une future interface de compatibilité pourra enrichir Release avec un protocole, mais ne doit bloquer le solo qu’à la demande explicite du produit.
- Décor vectoriel et logo typographique provisoires. Ajoutez Assets/background.png pour personnaliser l’illustration. Aucun asset généré supplémentaire n’est nécessaire pour compiler.
- Les sauvegardes créées par le jeu réel et son nom exact d’exécutable devront être vérifiés lors de la recette avec l’export Windows.
