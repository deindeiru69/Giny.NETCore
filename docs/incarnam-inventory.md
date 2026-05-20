# Incarnam — Audit de peuplement

Inventaire automatisé via `tools/IncarnamScan/` (lecture D2O + D2I client Dofus 2.68).

**Dernière exécution** : 2026-05-20 — données JSON dans `tools/IncarnamScan/output/`.

## TL;DR

- **Area Incarnam** : `AreaId = 45` (nom "Incarnam", SuperAreaId=3)
- **12 SubAreas** réparties sur **118 maps**
- **57 NPCs** référencés par les SubAreas (quest-givers et NPCs de scénario)
- **20 quêtes** ancrées à Incarnam, soit ~100 objectives au total
- **Handlers serveur** (post-commit `e1e8f581`) : 7 sur 9 types implémentés. Coverage **65/80 = 81%** des objectives runtime-trackables. Seul gap restant : `CraftItem` (15 occurrences).
- **Positions natives NPCs** : **48/57 récupérées via Doflex** (`tools/wiki_npc_scraper.py`). Dump : `tools/IncarnamScan/output/npc-positions-wiki.json`. Cf. section 7bis. Les 9 restants (1 téléporteur skippé + 8 introuvables/hors-Incarnam) restent en fallback spawn map `154010883`.

---

## 1. SubAreas Incarnam

| SubArea | Name | Level | Maps | NPCs (entries) | Quests (entries) |
|---|---|---|---|---|---|
| 442 | Lac | 5 | 15 | 9 | 0 |
| 443 | Forêt | 5 | 10 | 6 | 1 |
| 444 | Champs | 5 | 15 | 13 | 7 |
| 445 | Pâturages | 5 | 9 | 4 | 1 |
| 446 | Temple Céleste | 1 | 24 | 21 | 1 |
| 447 | Crypte de Kardorim | 10 | 8 | 1 | 0 |
| 448 | Taverne | 1 | 2 | 5 | 0 |
| 449 | Cimetière | 10 | 8 | 1 | 0 |
| **450** | **Route des âmes** (spawn map 154010883 ici) | 1 | 9 | 11 | 10 |
| 536 | Tutoriel guidé | 1 | 5 | 1 | 0 |
| 778 | Mine | 5 | 7 | 0 | 0 |
| 815 | Queue du Dragon | 120 | 6 | 4 | 0 |

→ Dump complet : `tools/IncarnamScan/output/subareas.json`

---

## 2. Maps

**118 maps** au total. Map de spawn confirmée : `154010883` (Route des âmes, SubArea 450).

Liste complète + coordonnées dans `tools/IncarnamScan/output/maps.json`.

---

## 3. NPCs (57 templates)

Sélection des NPCs les plus riches en dialogue (top par `DialogMessagesCount`) :

| TemplateId | Name | DialogMessages | Notes |
|---|---|---|---|
| 2205 | Mériana | 254 | Très bavarde, probable hub central |
| 1515 | Fécaline la Sage | 16 | Connue : NPC quête Dofus Sage |
| 1223 | Fée Risette | 15 | Tutoriel |
| 862 | Milicien Kerubim Nybi | 9 | |
| 890 | Habitué de la taverne | 5 | SubArea Taverne |
| 871 | Laura | 3 | |
| ... | ... | ... | (51 autres) |

**Note importante** : **Ganymède (4823) n'apparaît PAS dans cette liste.** Vérification : la liste vient de `SubArea.Npcs` (champ Ankama qui liste les "NPCs marqueurs" de la sous-zone — questgivers + figures importantes). Ganymède est probablement placé directement via `Maps.d2p` (binaires), pas via le D2O. C'est cohérent avec son rôle d'onboarding (spawn auto-tutoriel, pas questgiver classique).

Liste complète : `tools/IncarnamScan/output/npcs.json`

### `ActionsAuthorized` sur chaque NPC

Tous les NPCs scannés ont `ActionsAuthorized = [3]` = `TALK` uniquement. Cohérent avec le rôle "dialogue tutoriel". Aucun marchand / banquier / zaap dans Incarnam (logique : c'est une zone d'apprentissage hors-économie).

---

## 4. Quêtes (20, tri par LevelMin)

| QuestId | Name | LevelMin | LevelMax | StartCriterion | Steps |
|---|---|---|---|---|---|
| 1631 | Réponses à tout | 2 | 2 | `Qf=1630` (suite quête 1630) | 1 |
| 1639 | Transport peu commun | 3 | 3 | `BT=1` (auto-start) | ? |
| 1642 | Mise à l'épreuve | 3 | 3 | ? | ? |
| 1634 | Espoirs et tragédies | 5 | 5 | ? | ? |
| 1640 | Des vestiges de légende | 5 | 5 | ? | ? |
| 1643 | Champs de bataille | 5 | 5 | ? | ? |
| 1649 | Produits naturels | 5 | 5 | ? | ? |
| 1641 | Vu du ciel | 6 | 6 | ? | ? |
| 1644 | Coups d'épée dans l'eau | 6 | 6 | ? | ? |
| 1650 | La hache et la pioche | 6 | 6 | ? | ? |
| 1655 | Un peu de pigment | 6 | 6 | ? | ? |
| 1637 | La galette secrète | 7 | 7 | ? | ? |
| 1645 | Décime-moi des bouftous | 7 | 7 | ? | ? |
| 1651 | Boune un jour, boune toujours | 7 | 7 | ? | ? |
| 1635 | Dans la gueule du Milimilou | 8 | 8 | ? | ? |
| ... | (5 autres) | | | | |

Détail complet de chaque step + objective dans `tools/IncarnamScan/output/quests.json`.

### Note sur la quête 1630 ("Sous le regard des dieux")

La quête 1630 référencée par le `StartCriterion` de la 1631 n'apparaît pas dans les 20 quêtes scannées. Hypothèse : elle est sans doute dans une autre SubArea (probablement la "Quête tutoriel meta" ou "Première heure du jeu" globale) — à scanner séparément si besoin. C'était l'IDs proposée par l'agent dans la session précédente — confirmé partiellement (1630 existe et déclenche 1631).

---

## 5. Distribution des objective types (~100 objectives sur 20 quêtes)

| Type | Count | Handler Giny | Notes |
|---|---|---|---|
| **None** (0) | 20 | n/a | Flavor text / instruction journal. `Param0 = i18nKey`. Pas un objective trackable, juste descriptif. Compté ici pour transparence mais non-bloquant |
| **DefeatMonsterOneFight** (6) | 16 | ❌ | Battre N monstres en un seul combat |
| **BringItemToNpc** (2) | 16 | ❌ | Apporter un item à un NPC |
| **CraftItem** (17) | 15 | ❌ | Crafter un item |
| **NpcTalkBack** (9) | 13 | ✅ | `QuestManager.OnNpcTalk` |
| **GoToNpc** (1) | 11 | ✅ | `QuestManager.OnNpcTalk` |
| **DefeatMulti** (14) | 5 | ❌ | Battre plusieurs monstres cumulés |
| **GiveItemToNpc** (3) | 3 | ❌ | Donner un item à un NPC |
| **DiscoverMap** (4) | 1 | ❌ | Découvrir une map spécifique |

### Calcul des objectives traquables

- **Total objectives** : 100 (sur 20 quêtes)
- **Type=None (descriptif, non-bloquant)** : 20 → quasi-implicite, n'empêche pas la progression
- **Traquables aujourd'hui** (GoToNpc + NpcTalkBack) : 24
- **Manquants** : 56 (DefeatMonster*, BringItemToNpc, CraftItem, GiveItemToNpc, DiscoverMap)

→ **Aujourd'hui sans nouveau handler, ~44% des objectives sont gérés** (les 24 GoTo/TalkBack + 20 flavor None). 56% des objectives sont bloquants — la quête peut être démarrée et dialoguer, mais pas validée.

---

## 6. Plan d'implémentation priorisé

### Ordre suggéré (rapport débloquage/effort)

| # | Handler | Occurrences | Effort estimé | Débloque |
|---|---|---|---|---|
| 1 | `BringItemToNpc` (2) | 16 | 1-2h (hook sur `OpenNpcTradeExchange` + check inventaire) | Quêtes de livraison (majorité tutoriel) |
| 2 | `DefeatMonsterOneFight` (6) | 16 | 2-3h (hook `Fight.OnFightEnding` + filtre monsterId) | Quêtes "tue X bouftous" |
| 3 | `GiveItemToNpc` (3) | 3 | partagé avec #1 (~30 min de plus) | Quêtes "donne l'objet" |
| 4 | `DefeatMulti` (14) | 5 | similaire à #2 (~1h) | Quêtes "tue X au total" |
| 5 | `CraftItem` (17) | 15 | 2-3h (hook `Craft.OnSuccess`) | Quêtes craft tutoriel |
| 6 | `DiscoverMap` (4) | 1 | 1h (hook `Character.OnEnterMap`) | 1 quête d'exploration |

**Plan A — débloquer le tutoriel principal** : implémenter #1 + #2 + #3 (~4h de code + tests). Débloque ~35 objectives = ~35% du contenu Incarnam.

**Plan B — peuplement minimal "dialogue only"** : ne rien coder de plus. Limiter le contenu mis en DB à des quêtes 100% GoToNpc/NpcTalkBack. Suffit pour une démo "balade dans Incarnam, parler aux PNJs", insuffisant pour tutoriel complet.

**Plan C — full** : tout implémenter (#1 à #6). ~10-12h de code total. Incarnam pleinement jouable.

### Schéma d'implémentation des handlers manquants

Pattern proposé (à factoriser depuis `QuestManager.OnNpcTalk`) :

```csharp
// QuestManager.cs - nouveau
public void OnMonsterDefeated(Character character, MonsterRecord monster, Fight fight)
{
    foreach (var quest in character.GetActiveQuests())
    {
        // DefeatMonsterOneFight : tous les monstres requis dans le même fight
        var oneFightObj = quest.GetObjectives(QuestObjectiveTypeEnum.DefeatMonsterOneFight)
            .FirstOrDefault(o => !o.Done && o.Record.Parameters.Param0 == monster.Id);
        if (oneFightObj != null && fight.MonstersOfType(monster.Id) >= oneFightObj.Record.Parameters.Param1)
            character.CompleteQuestObjective(quest, oneFightObj);

        // DefeatMulti : compteur incrémental
        var multiObj = quest.GetObjectives(QuestObjectiveTypeEnum.DefeatMulti)
            .FirstOrDefault(o => !o.Done && o.Record.Parameters.Param0 == monster.Id);
        if (multiObj != null) {
            multiObj.Counter++;
            if (multiObj.Counter >= multiObj.Record.Parameters.Param1)
                character.CompleteQuestObjective(quest, multiObj);
        }
    }
}

public void OnItemGiven(Character character, Npc npc, ItemRecord item, int quantity)
{
    foreach (var quest in character.GetActiveQuests())
    {
        var obj = quest.GetObjectives(QuestObjectiveTypeEnum.BringItemToNpc, QuestObjectiveTypeEnum.GiveItemToNpc)
            .FirstOrDefault(o => !o.Done
                && o.Record.Parameters.Param0 == npc.Template.Id
                && o.Record.Parameters.Param1 == item.Id
                && quantity >= o.Record.Parameters.Param2);
        if (obj != null)
            character.CompleteQuestObjective(quest, obj);
    }
}
```

À hooker depuis :
- `Fight.OnFightEnding` (pour chaque monstre tué par le character)
- `Character.OpenNpcTradeExchange` + son commit (quand le joueur donne un item)
- `Character.OnEnterMap` (DiscoverMap)
- `Craft.OnSuccess` (CraftItem)

Le `CharacterQuestObjectiveRecord` actuel n'a pas de champ `Counter` — à ajouter pour DefeatMulti.

---

## 7. Sources investiguées pour les positions natives NPCs

État : **aucune source automatisée disponible**.

### Pcap Ankama
Impossible — Dofus 2 (classic) est fermé, plus de serveur Ankama actif depuis lequel sniffer.

### Dumps SQL communautaires
Plusieurs leads vérifiés, aucun pertinent pour Dofus 2.x complet :
- **Stump** : repo principal 404, miroirs partiels sans données monde
- **Otomai** : pré-alpha, contenu lacunaire
- **Araknemu** : ciblé Dofus 1.29 (Retro), schéma incompatible
- **Autres** : false leads sur fragments anciens / versions incompatibles

### Maps.d2p / .dlm (testé Phase 0 ce commit)

Hypothèse testée : "les .dlm contiennent une référence aux NPCs natifs Ankama, juste non-parsée par Giny.IO".

**Verdict : invalidée.** Preuves via `dotnet run -- --dump-dlm 154010883` (Place d'Incarnam, le spawn) :

| Test | Résultat |
|---|---|
| Parse OK | ✅ MapVersion=11, Id=154010883, SubAreaId=450 |
| Element types présents | **GraphicalElement (988) et SoundElement (0) uniquement** |
| Element types inconnus | 0 (le parser throw sur unknown — il aurait crashé si NPC existait) |
| Round-trip parse → reserialize | 24 690 bytes → 24 690 bytes, **byte-equivalent à 4 bytes près** (encryption metadata) |
| NPC TemplateIds cherchés (58 IDs Incarnam) | **0 match** dans `ElementId` ou `Identifier` |
| Côté serveur Giny `SpawnNpcs` | Itère **uniquement** `npc_spawns` DB. Aucun fallback `.dlm` |

→ Le format .dlm contient le décor (terrain + fixtures + élements graphiques + ambiance sonore) et la topologie (cellules + voisins). **Aucune référence aux acteurs gameplay** (NPCs, monstres, ressources). Côté Ankama, ces données vivent sur le serveur de jeu, jamais distribuées au client. C'est cohérent avec la sécurité du contenu (un client compromis ne révèle pas le placement des spawns).

### Conclusion stratégique

Les positions ne sont pas dans le client. Mais elles sont disponibles publiquement sur **Doflex** (encyclopédie communautaire Dofus). Cf. section 7bis pour le scraper et la couverture obtenue.

Le mini-probe `--dump-dlm <mapId>` est gardé dans `tools/IncarnamScan/DlmProbe.cs` pour de futures investigations structurelles d'autres maps si besoin.

---

## 7bis. Positions NPCs reconstruites via Doflex

Script : `tools/wiki_npc_scraper.py` (Python 3, deps : `requests`).
Output : `tools/IncarnamScan/output/npc-positions-wiki.json` (57 entrées, schéma fixé).

### Stratégie

1. Crawl complet de l'index Doflex (`https://doflex.fr/fr/encyclopedia/npcs?page=N`, ~115 pages × 50 NPCs = ~5700 NPCs au total) → index local `{nom_normalisé : [(id_doflex, url)]}`.
2. Pour chaque NPC d'`npcs.json` (57 templates) : lookup nom normalisé (sans accents, lowercase) → fetch page détail Doflex → parse premier `card-a-name` de la section "Positions" pour récupérer `SubAreaLabel [X, Y]` + parent label.
3. Résolution `(X, Y) → MapId` via `maps.json` : préférence aux maps dont `SubAreaName` matche la SubArea Doflex (substring case-insensitive), puis fallback Outdoor=true.
4. Fallback explicite (`source = "fallback-spawn-map"`, MapId = 154010883) si : NPC introuvable sur Doflex, page sans Positions, ou coords hors-Incarnam.
5. Hard skip pour le NPC 4398 ("Portail vers Astrub", téléporteur objet) → `source = "skipped-teleporter"`.

Garde-fous :
- **Pas d'invention** : chaque coord vient d'une page tierce, et le mapping doit hit une vraie map Incarnam.
- Rate limit 500ms entre requêtes, 3 retries.
- Fallback secondaire `wiki-dofus.eu` codé mais non-utilisé (SSL cert mismatch du domaine — 0 hit lors du run).

### Couverture obtenue (run 2026-05-20)

| Source | Count | % |
|---|---|---|
| **doflex** | **48** | **84.2%** |
| `wiki-dofus-eu` | 0 | 0% (SSL cert mismatch, retries échouent) |
| `skipped-teleporter` | 1 | 1.8% (NPC 4398) |
| `fallback-spawn-map` | 8 | 14.0% |
| **Total** | **57** | **100%** |

### Breakdown Doflex par SubArea Incarnam (48 NPCs résolus)

| SubArea Doflex | NPCs |
|---|---|
| Champs | 12 |
| Route des âmes | 9 |
| Lac | 9 |
| Taverne | 5 |
| Forêt | 5 |
| Pâturages | 4 |
| Queue du Dragon | 2 |
| Cimetière | 1 |
| Temple Céleste | 1 |

### Fallbacks détaillés (8 NPCs)

| TemplateId | Name | Statut |
|---|---|---|
| 2207 | Orbalantyr | Pas de match Doflex |
| 2895 | Maître Hoboulo | Pas de match Doflex |
| 2896 | Maître Darm | Pas de match Doflex |
| 2904 | Oskar Khas | Pas de match Doflex |
| 2936 | Kardorim | Match Doflex (id=2248) mais sa fiche n'a pas de Positions listées |
| 3687 | Orbalantyr de Mériana | Match Doflex (id=2944) mais position [-6,-3] dans "Marécages nauséabonds" (hors Incarnam) → fallback |
| 5336 | Goultard | Match Doflex (id=638) mais position [-6,-12] dans "Arènes de Goultard" (hors Incarnam) → fallback |
| 7102 | Andrée Inkarnay | Pas de match Doflex |

→ Ces 8 NPCs devront être placés manuellement (`.addnpc` + override DB) si on veut un peuplement 100%. Pour Goultard et Orbalantyr de Mériana, leur apparition à Incarnam est probablement spécifique à un script de quête (le NPC a une position canonique ailleurs).

### Format de sortie

```json
[
  {
    "templateId": 2205,
    "name": "Mériana",
    "mapId": 154010883,
    "cellId": 280,
    "direction": 2,
    "source": "doflex",
    "url": "https://doflex.fr/fr/encyclopedia/npcs/...",
    "coords": "0,-3",
    "subArea": "Taverne"
  }
]
```

- `cellId` et `direction` restent en valeurs par défaut (280 / 2) — Doflex ne les expose pas. À ajuster manuellement post-import si besoin via WorldEditor.
- `mapId` = soit la map Outdoor matchant `(X, Y)` + `subArea`, soit `154010883` (spawn) en fallback.

### Comment relancer le scraper

```bash
cd tools/
python wiki_npc_scraper.py
```

Durée : ~2 min (115 pages d'index + 57 fetches détail + retries). Le JSON output est régénéré ; diff via `git diff IncarnamScan/output/npc-positions-wiki.json` pour comparer.

### Prochaine étape

INSERT les 48 spawns Doflex en DB via un patcher idempotent dans `Sources/Modules/Giny.DatabasePatcher/` (cf. section 10), keyed par `(templateId, mapId)` pour ne pas dupliquer en cas de re-run.

---

## 8. Comment refaire ce scan

```bash
cd tools/IncarnamScan
dotnet run                                 # scan principal
dotnet run -- "D:\Path\to\Dofus"           # client custom
dotnet run -- --probe                      # exploratoire (areas, recherche substring)
dotnet run -- --dump-dlm 154010883         # analyse structurelle d'un .dlm
```

Les JSON sont régénérés dans `tools/IncarnamScan/output/`. Diff via `git diff output/` pour voir l'évolution.

Quand on ajoutera des handlers (étape 6 plan ci-dessus), il faudra :
1. Mettre à jour `ImplementedObjectiveTypes` dans `Program.cs` pour refléter les nouveaux handlers.
2. Re-run le scanner.
3. Les JSON refléteront le `HandlerImplemented: true` correspondant.

---

## 9. État DB sur la VM (à compléter)

SSH non disponible depuis l'environnement de scan. À exécuter depuis ton poste :

```bash
ssh deindeiru@deindeiruworld.duckdns.org \
  "sudo mysql deindeiruworld -e 'SELECT * FROM npc_spawns; SELECT * FROM npc_actions; SELECT * FROM npc_replies;'"
```

À coller ci-dessous une fois récupéré :

```
[à compléter]
```

---

## 10. Workflow de peuplement

Avec les handlers du commit `e1e8f581` en place et **aucune source automatique pour les positions** (cf. section 7) :

1. **Placer les NPCs** un par un :
   - `.addnpc <templateId>` in-game sur la cellule du character (rapide mais fastidieux × 57)
   - Ou WorldEditor : éditer `NpcSpawnRecord.cellId/direction/mapId` à la main
   - Ou INSERT SQL direct dans `npc_spawns` si on a une liste cellId/mapId externe (wiki, anciens screenshots)
2. **Pour chaque NPC** : créer ses `NpcActionRecord` TALK avec `Param1 = <npcMessageId>` choisi parmi son `DialogMessages` D2O (cf. SelectNpcMessageDialog dans WorldEditor)
3. **Pour les 20 quêtes** : INSERT en DB des `QuestRecord` + `QuestStepRecord` + `QuestObjectiveRecord` correspondants. Possible via le DatabaseSynchronizer si on régénère, mais celui-ci écrase tout — risque sur les patches custom existants. Recommandé : un patcher dédié dans `Sources/Modules/Giny.DatabasePatcher/` qui INSERT uniquement les 20 quêtes Incarnam, idempotent.
4. **Tester** : créer un perso, suivre la chaîne de quêtes, vérifier que chaque objective tracke correctement (les 7 types implémentés + Type=None descriptif).
