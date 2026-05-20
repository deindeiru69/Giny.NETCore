# Diagnostic du système de quêtes Giny.NETCore

**Date** : 2026-05-20
**Branche** : `hero-mode`
**Contexte** : 55 NPCs Incarnam ont été insérés en DB (commit `4cd5b499`),
1929 quêtes / 15379 objectives sont chargés en mémoire, **mais aucune quête
ne se déclenche en jeu**. Ce document trace exactement comment une quête
démarre dans le code actuel et identifie ce qu'il manque.

Lecture seule. Aucun fichier de `Sources/` modifié pendant ce diagnostic.

---

## Q1 — Comment une quête démarre actuellement

### Méthode publique unique

`Character.StartQuest(long questId)` à
[Character.cs:563](../Sources/Servers/Giny.World/Managers/Entities/Characters/Character.cs#L563-L585) :

```csharp
public bool StartQuest(long questId)
{
    if (Record.Quests.Any(x => x.QuestId == questId)) return false;
    QuestRecord record = QuestRecord.GetQuest(questId);
    if (record == null) return false;
    var characterQuest = QuestManager.Instance.CreateCharacterQuest(record);
    Record.Quests.Add(characterQuest);
    Client.Send(new QuestStartedMessage((short)characterQuest.QuestId));
    TextInformation(TextInformationTypeEnum.TEXT_INFORMATION_MESSAGE, 54, characterQuest.QuestId);
    return true;
}
```

Garde-fous : refuse si la quête est déjà dans `Record.Quests` (déjà active
ou déjà terminée — pas de check Finished séparé) ou si le `QuestId` n'existe
pas en mémoire.

**Aucune vérification de `LevelMin` / `LevelMax` / `StartCriterion`** dans cette
méthode — c'est à l'appelant de filtrer.

### Call-sites de `Character.StartQuest()`

Grep complet : **2 appelants**.

| # | Caller | Fichier:ligne | Trigger |
|---|---|---|---|
| 1 | `GenericActions.HandleStartQuest(character, reply)` | [GenericActions.cs:54-57](../Sources/Servers/Giny.World/Managers/Generic/GenericActions.cs#L53-L57) | Dispatché quand un `npc_reply` a `ActionIdentifier = GenericActionEnum.StartQuest` et `Param1 = "<questId>"` |
| 2 | `ChatCommands` cmd `.quest <id>` | [ChatCommands.cs:509-513](../Sources/Servers/Giny.World/Managers/Chat/ChatCommands.cs#L509-L513) | Commande admin (`ServerRoleEnum.Administrator` requis) |

Il n'existe **aucun call automatique** (pas d'auto-start au login, au TALK,
à l'arrivée sur une map, ni à un quelconque event runtime).

### Call stack complet pour le chemin nominal (NPC dialogue → StartQuest)

```
Client clique sur un NPC (TALK)
  └─> Character.TalkToNpc(npc, action)                       Character.cs:795-799
        └─> OpenDialog(new NpcTalkDialog(this, npc, action))
        └─> QuestManager.Instance.OnNpcTalk(this, npc)       (tracking only, ne démarre rien)

NpcTalkDialog.Open() envoie NpcDialogQuestionMessage
  affichage côté client : messageId + réponses dispo

Client clique une réponse
  └─> NpcTalkDialog.Reply(replyId)                           NpcTalkDialog.cs:72-89
        └─> foreach reply where reply.ActionIdentifier != None:
              GenericActionsManager.Instance.Handle(Character, reply)
                └─> Si ActionIdentifier == StartQuest:
                      GenericActions.HandleStartQuest(character, reply)   GenericActions.cs:53-57
                        └─> character.StartQuest(long.Parse(reply.Param1))
                              └─> QuestManager.CreateCharacterQuest(record)
                              └─> Record.Quests.Add(characterQuest)
                              └─> Client.Send(QuestStartedMessage(...))
```

**Le mécanisme est entièrement en place. Le SQL généré au commit `4cd5b499`
n'utilise PAS ce mécanisme** : tous les `npc_replies` insérés ont
`ActionIdentifier = 0` (GenericActionEnum.None), ce qui ferme le dialogue
sans déclencher quoi que ce soit.

---

## Q2 — `GenericActionEnum` : valeurs disponibles

Fichier : [GenericActionEnum.cs](../Sources/Servers/Giny.World/Managers/Generic/GenericActionEnum.cs)

```csharp
public enum GenericActionEnum
{
    None,           // 0
    Teleport,       // 1
    OpenBank,       // 2
    RemoveItem,     // 3
    LearnOrnament,  // 4
    LearnTitle,     // 5
    Collect,        // 6
    Bidshop,        // 7
    Zaap,           // 8
    Zaapi,          // 9
    CreateGuild,    // 10
    LearnSpell,     // 11
    AddKamas,       // 12
    Craft,          // 13
    AddItem,        // 14
    AddExperience,  // 15
    Notification,   // 16
    Fight,          // 17
    Smithmagic,     // 18
    RuneTrade,      // 19
    PokefusWish,    // 20
    Unhandled,      // 21
    Paddock,        // 22
    StartQuest,     // 23   ← PRÉSENT
    ContinueDialog, // 24
}
```

**`StartQuest` existe.** Pas de `EndQuest`, `BeginQuest`, `Quest`, ni
`FinishQuest`. La logique "finir une quête" se fait implicitement quand
tous les objectifs sont validés (cf.
[Character.CompleteQuestObjective](../Sources/Servers/Giny.World/Managers/Entities/Characters/Character.cs#L599-L621)),
pas via un ActionIdentifier dédié.

---

## Q3 — Handlers `[GenericActionHandler]`

Tous les handlers sont définis dans
[GenericActions.cs](../Sources/Servers/Giny.World/Managers/Generic/GenericActions.cs) :

| Action | Handler implémenté | Notes |
|---|---|---|
| `None` (0) | n/a | Pas dispatché (filtre `!= None` dans NpcTalkDialog) |
| `Teleport` | ✅ Param1=mapId, Param2=cellId optionnel |
| `OpenBank` | ✅ |
| `RemoveItem` | ✅ Param1=itemId, Param2=qty |
| `LearnOrnament` | ✅ Param1=ornamentId |
| `LearnTitle` | ✅ Param1=titleId |
| `Collect` | ✅ Utilisé par MapStatedElement |
| `Bidshop` | ✅ Param1=bidShopId |
| `Zaap` | ✅ |
| `Zaapi` | ✅ |
| `CreateGuild` | ✅ |
| `LearnSpell` | ✅ Param1=spellId |
| `AddKamas` | ✅ Param1=amount |
| `Craft` | ✅ |
| `AddItem` | ✅ Param1=itemId, Param2=qty |
| `AddExperience` | ✅ Param1=xp |
| `Notification` | ✅ Param1=text |
| `Fight` | ✅ Param1=monsterIds CSV |
| `Smithmagic` | ✅ |
| `RuneTrade` | ✅ |
| `PokefusWish` | ❌ orphelin (handler absent) |
| `Unhandled` | ✅ Fallback explicite |
| `Paddock` | ❌ orphelin (handler absent) |
| **`StartQuest`** | **✅ Param1=questId — `character.StartQuest(long.Parse(...))`** |
| `ContinueDialog` | ✅ Param1=messageId — relance le dialog NPC |

**Conclusion Q3** : pour les quêtes, **un seul handler nous concerne :
`StartQuest`**, et il est entièrement implémenté. Pas de gap côté code.

---

## Q4 — Parseur `StartCriterion`

### Champ DB

`QuestRecord.StartCriterion` existe (string) à
[QuestRecord.cs:100](../Sources/Servers/Giny.World/Records/Quests/QuestRecord.cs#L99-L104),
mappé sur le D2OField `"startCriterion"`. Chargé en mémoire au démarrage,
**mais jamais lu**. Grep complet "StartCriterion" → 1 seule occurrence dans
tout `Giny.World` : la définition elle-même.

### Système de criterion existant (utilisé pour replies, items, etc.)

[`CriteriasManager`](../Sources/Servers/Giny.World/Managers/Criterions/CriteriasManager.cs)
+ [`CriteriaExpression`](../Sources/Servers/Giny.World/Managers/Criterions/CriteriaExpression.cs)
+ 24 handlers dans `Handlers/`.

Mécanisme :
- Une criterion atomique = chaîne de 3+ caractères : `"XX<op><val>"`
- 2 premiers caractères = identifier (clé dans `CriteriasManager.m_handlers`)
- 3e caractère = opérateur (`<`, `>`, `!`, `=`, `X`, `~`)
- Reste = valeur
- Expressions composées via `&` (AND) et `|` (OR) avec gestion parenthèses

### Identifiers supportés (24 handlers)

| ID | Handler | Sémantique |
|---|---|---|
| `PL` | LevelCriterion | `client.Character.Level` |
| `ST` | StrengthCriterion | Force |
| `AG` | AgilityCriterion | Agilité |
| `CH` | ChanceCriterion | Chance |
| `SA` | WisdomCriterion | Sagesse |
| `IN` | IntelligenceCriterion | Intelligence |
| `VI` | VitalityCriterion | Vitalité |
| `PA` | ActionPointsCriterion | PA |
| `PM` | MovementPointsCriterion | PM |
| `BR` | BreedCriterion | Classe |
| `Eo` | (ornament) | HasOrnament |
| `Ee` | (emote) | HasEmote |
| `Ht` | (title) | HasTitle |
| `Hs` | (spell) | HasSpell |
| `Hi` | HasItemCriterion | Has item |
| `St` | HasStateCriterion | État |
| `Is` | ItemSetCriterion | Item set |
| `Ne` | NotEquipableCriterion | |
| `Sb` | SubscribedCriterion | Abonné |
| `Gu` | GuildCriterion | Guilde |
| `Al` | AlignmentCriterion | Alignement |
| `Km` | KillMonsterWithChallengeCriterion | |
| `Ac` | AchievementPointsCriterion | |

### **Identifiers absents critiques pour les quêtes**

| ID Ankama | Sémantique | Présent ? |
|---|---|---|
| `BT` | Battle Tutorial ? (booléen tutoriel) | ❌ |
| `Qf` | Quest Finished | ❌ |
| `Qa` | Quest Active | ❌ |
| `Qp` | Quest Progress | ❌ |
| `QF` / `QF` variantes | | ❌ |
| `Of` | Objective Finished | ❌ |

### Comportement en cas d'identifier inconnu

`CriteriasManager.GetCriteriaHandler` retourne
[`UnknownCriterion`](../Sources/Servers/Giny.World/Managers/Criterions/Handlers/UnknownCriterion.cs)
si l'identifier n'est pas mappé, et cet handler **renvoie toujours `true`** :

```csharp
public override bool Eval(WorldClient client) { return true; }
```

**Implication** : même si on câblait l'évaluation de `StartCriterion`,
toutes les conditions `BT=1` / `Qf=1629` / `Qa=...` seraient considérées
satisfaites par défaut. Donc :
- **Côté positif** : un patch minimal qui lit `StartCriterion` ne casserait
  rien — toutes les criterions inconnues passent.
- **Côté négatif** : on ne saura pas distinguer une quête vraiment éligible
  d'une quête prerequise — la chaîne `1630 → 1631 → 1634` n'aura aucun ordre
  imposé.

---

## Q5 — Synthèse "ce qui existe / ce qui manque"

### ✅ Ce qui existe déjà

| Brique | Statut |
|---|---|
| `QuestRecord` chargé en DB depuis D2O (Quests.d2o) | ✅ 1929 quêtes en mémoire |
| `QuestStepRecord` / `QuestObjectiveRecord` cascadés | ✅ |
| `Character.StartQuest(questId)` (création de l'instance + envoi message) | ✅ |
| `QuestManager.CreateCharacterQuest(record)` (instancie depuis Step[0]) | ✅ |
| `QuestManager.OnNpcTalk` (tracking GoToNpc, NpcTalkBack, BringItem, GiveItem) | ✅ |
| `QuestManager.OnFightEnded` (DefeatMonsterOneFight, DefeatMulti) | ✅ |
| `QuestManager.OnMapDiscovered` (DiscoverMap) | ✅ |
| `QuestManager.ApplyRewards` (items, titles, emotes, kamas, spells) | ✅ |
| Persistence `CharacterQuestRecord` via ProtoBuf | ✅ |
| `npc_replies.ActionIdentifier = StartQuest` → `Character.StartQuest()` | ✅ wired via GenericActions |
| Marqueur "!" jaune au-dessus des NPCs questgivers (`GameRolePlayNpcQuestFlag`) | ✅ basé sur `NpcReplyRecord.GetQuestsFromSpawnId` qui scanne `ActionIdentifier=StartQuest` |
| Commande admin `.quest <id>` (test rapide) | ✅ |
| Système criterion générique (`PL>8` → `LevelCriterion`) | ✅ |
| Identifiers criterion non-stats : Eo, Ee, Ht, Hs, Hi, Sb, etc. | ✅ |
| Évaluation de `NpcReplyRecord.Criteria` à l'affichage | ✅ |
| Évaluation de `NpcActionRecord.Criteria` à l'interaction | ✅ |

### ❌ Ce qui manque

| Brique | Sévérité | Détails |
|---|---|---|
| `npc_replies` peuplés avec `ActionIdentifier=StartQuest` | **bloquant** | Le SQL `incarnam-npcs.sql` met `ActionIdentifier=0` (None). Aucune quête ne peut donc être proposée. C'est la cause directe de "rien ne se déclenche". |
| Mapping NPC → quêtes qu'il propose | **bloquant** | Pour chaque NPC questgiver, il faut savoir : (a) quel questId proposer, (b) sur quel `MessageId` mettre le bouton "Accepter". La donnée Ankama est dans D2O (`Quest.Steps[0].Objectives` + `Npc.dialogReplies`) mais pas exploitée par `IncarnamSqlGen` actuellement. |
| Lecture de `QuestRecord.StartCriterion` | mineur | Champ chargé mais ignoré. Pour Incarnam (zone tutoriel), peu critique : les criterions Ankama sont souvent `Qf=<prevQuest>` qui de toute façon retournerait `true` (UnknownCriterion). |
| Handlers criterion `BT`, `Qf`, `Qa`, `Qp`, `Of` | mineur | Tant que rien ne lit `StartCriterion`, l'absence ne se voit pas. Si un jour on veut "quête 1631 dispo seulement après avoir fini 1630", il faudra `QfCriterion`. |
| Auto-trigger au TALK (scan des quêtes éligibles) | optionnel | Pas dans le design Ankama natif (Ankama force le joueur à cliquer "Accepter"). Mais utile en mode "onboarding rapide". |

### 🔧 Estimation d'effort

| Plan | Description | Effort | Risques |
|---|---|---|---|
| **A** | Régénérer le SQL pour chaque NPC questgiver Incarnam : trouver le questId proposé, son `startMessageId`, INSERT un `npc_replies` avec `ActionIdentifier=StartQuest` `Param1=<questId>` au lieu du reply "Au revoir." actuel | **2-4h** | Faible. Mécanisme déjà testé sur d'autres zones (probable). Risque : mauvais MessageId → reply ne s'affiche pas, mais ne crashe rien |
| **B** | Coder un trigger auto dans `QuestManager.OnNpcTalk` : scanner `QuestRecord.GetQuests()`, filtrer (LevelMin OK + non-active + premier objective = GoToNpc avec ce NPC.TemplateId) et appeler `Character.StartQuest()` automatiquement | **3-5h** | Moyen. S'écarte du flow Ankama (le joueur reçoit la quête sans cliquer). Risque secondaire : performance — scan O(1929) à chaque TALK. Doit indexer par NPC. |
| **C** | A + B (peuplement DB + filet de sécurité auto-start pour les NPCs où on a raté le mapping) | **4-6h** | A est suffisant si on a les données. B est utile en complément pour les NPCs où le mapping startMessageId est ambigu. |

---

## Q6 — Le code supporte-t-il l'auto-trigger au TALK ?

**Réponse courte : Non.**

`QuestManager.OnNpcTalk()` est appelé à chaque TALK
([Character.cs:798](../Sources/Servers/Giny.World/Managers/Entities/Characters/Character.cs#L795-L799))
mais son contenu actuel
([QuestManager.cs:30-66](../Sources/Servers/Giny.World/Managers/Quests/QuestManager.cs#L30-L66))
**ne fait que tracker les objectifs des quêtes DÉJÀ ACTIVES** :

```csharp
public void OnNpcTalk(Character character, Npc npc)
{
    var quests = character.GetActiveQuests();   // ← uniquement les ACTIVES
    foreach (var quest in quests)
    {
        // GoToNpc / NpcTalkBack — valide l'objectif
        // BringItemToNpc / GiveItemToNpc — consomme item, valide
    }
}
```

Le scan "quêtes éligibles à démarrer" n'existe pas. Pour l'implémenter,
il faudrait y ajouter quelque chose comme :

```csharp
// Scan candidates to start :
foreach (var quest in QuestRecord.GetQuests())
{
    if (character.HasQuest((short)quest.Id)) continue;
    if (character.Level < quest.LevelMin) continue;
    if (character.Level > quest.LevelMax && quest.LevelMax > 0) continue;
    if (!CriteriaExpression.Eval(quest.StartCriterion, character.Client)) continue;

    // Premier objective doit être GoToNpc avec ce NPC
    var firstObj = quest.Steps.FirstOrDefault()?.Objectives.FirstOrDefault();
    if (firstObj == null) continue;
    if (firstObj.ObjectiveType != QuestObjectiveTypeEnum.GoToNpc) continue;
    if (firstObj.Parameters.Param0 != npc.Template.Id) continue;

    character.StartQuest(quest.Id);
    break;  // une seule auto-start par TALK
}
```

Cette implémentation supposerait :
- Index O(1) sur (NPC TemplateId → liste de quêtes dont le premier objective
  = GoToNpc avec ce NPC) pour éviter le scan O(1929) à chaque TALK (~5ms
  acceptable pour début, à optimiser si latency monte).
- Bonne synergie avec le code existant — pas de refactor de Character /
  QuestManager / Dialog.

**Localisation suggérée** : nouvelle méthode `QuestManager.TryAutoStartFromNpc(character, npc)` appelée au début de `OnNpcTalk`, avant le tracking des objectives existants.

---

## Recommandation

**Plan C (A + B)**.

- **Plan A seul** suppose qu'on a un mapping fiable `(NPC, questgiver MessageId)`
  pour chaque quête Incarnam. On ne l'a pas, et le construire à la main pour
  ~20 quêtes prend du temps. De plus, les "Quest Steps" Ankama peuvent forker
  (plusieurs NPC startgivers possibles).
- **Plan B seul** est plus simple mais s'écarte du flow Ankama : le joueur
  reçoit la quête sans dialogue d'acceptation, ce qui peut surprendre. Tout
  reste fonctionnel mais l'UX est non-canonique.
- **Plan C** : commencer par B (filet de sécurité universel — couvre les 20
  quêtes Incarnam en 1 commit), puis affiner avec A pour les questgivers
  où on veut le dialogue d'acceptation canonique.

### Ordre d'attaque proposé (si on procède)

1. **Phase 1** (~3h) — Implémenter `QuestManager.TryAutoStartFromNpc` avec
   index lazy par TemplateId. Couplé au déjà-câblé `OnNpcTalk`.
2. **Phase 2** (~1h) — Tester en jeu : `.addnpc 2896` (Maître Darm), TALK,
   vérifier qu'une quête de niveau 1 démarre.
3. **Phase 3** (~2h) — Si besoin, régénérer `incarnam-npcs.sql` pour mettre
   `ActionIdentifier=StartQuest` sur les replies des NPCs où on connait le
   `startMessageId` (questgivers principaux : Maître Darm, Fée Risette,
   Fécaline la Sage, Mériana).
4. **Phase 4** (optionnel) — Implémenter `QfCriterion` et `BTCriterion` si
   on veut une chaîne de pré-requis stricte. Pour l'instant, `UnknownCriterion`
   = always-true suffit.

### Risques principaux

- **Quêtes Ankama avec criterion non-trivial** (ex: `Qf=1630&PL>5`) :
  côté Giny, `Qf=1630` est always-true mais `PL>5` est strict. Si une quête
  exige PL>5 et le joueur est niveau 3 → refus.  Acceptable pour démo.
- **Quêtes avec premier objective ≠ GoToNpc** : `OnNpcTalk` ne les trigger
  pas. À identifier dans `quests.json`. Solution : étendre l'auto-trigger à
  d'autres types ou les marquer "manual start only".
- **Quêtes avec plusieurs NPCs candidats au premier objective** : prendre la
  première qui match (LevelMin croissant) suffira pour Incarnam.

---

## Annexes : où se trouvent les données

| Donnée | Source | Container runtime |
|---|---|---|
| Quêtes | `Quests.d2o` → `quests` table | `QuestRecord.Quests` (`Dictionary<long, QuestRecord>`) |
| Steps | `QuestSteps.d2o` → `quest_steps` | `QuestStepRecord` |
| Objectives | `QuestObjectives.d2o` → `quest_objectives` | `QuestObjectiveRecord`, `Parameters.Param0..Param4` |
| Rewards | `QuestStepRewards.d2o` → `quest_step_rewards` | `QuestStepRewardRecord` |
| NPC dialog | `Npcs.d2o` `dialogMessages` (List<List<int>>) | Pas chargé en DB côté Giny — les `npc_actions.Param1` (MessageId) sont fixés au peuplement |
| Spawn NPC | `npc_spawns` (DB Giny) | `NpcSpawnRecord` |
| Actions NPC (TALK/EXCHANGE/BUYSELL) | `npc_actions` (DB Giny) | `NpcActionRecord` |
| Replies dialog | `npc_replies` (DB Giny) | `NpcReplyRecord`, `ActionIdentifier` détermine l'effet |

### Données natives Ankama qu'on n'utilise pas (encore)

- `Npc.dialogReplies` : structure `List<List<int>>` parallèle à
  `dialogMessages` qui mappe les IDs de réponses possibles pour chaque
  message. Si on veut reproduire le dialogue exact d'un NPC officiel, il
  faut le décoder.
- `Quest.Steps[i].Objectives[j].Parameters.Param0..Param4` : on les
  exploite pour 7 types d'objectifs (cf. `docs/incarnam-inventory.md` §5),
  reste 2 types (`CraftItem`, `DiscoverSubarea`) non-câblés.

---

## TL;DR pour décideur

- Le code Giny **PEUT** démarrer une quête via TALK NPC, mais il faut
  insérer un `npc_replies` avec `ActionIdentifier=23` (`StartQuest`) et
  `Param1="<questId>"`. Notre SQL Incarnam actuel met `ActionIdentifier=0`
  → rien ne se passe.
- Le code Giny **NE PEUT PAS** auto-démarrer une quête éligible quand on
  parle à un NPC. Ce comportement n'existe pas, il faudrait l'ajouter
  (~3h dans `QuestManager.OnNpcTalk`).
- `QuestRecord.StartCriterion` est chargé en mémoire mais **jamais évalué**.
  Pas bloquant pour Incarnam (les criterions inconnues sont always-true),
  bloquant pour des zones avec chaînes de prérequis strictes.
- Recommandation : **Plan C** — coder l'auto-trigger (Plan B) en filet
  universel, puis régénérer le SQL avec les vrais `ActionIdentifier=StartQuest`
  (Plan A) pour les questgivers principaux.
