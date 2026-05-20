-- ============================================================
-- Incarnam NPC population (npc_spawns + npc_actions + npc_replies)
-- Generated  : 2026-05-20 18:20:05 UTC
-- Generator  : tools/IncarnamSqlGen/Program.cs
-- Inputs     : tools/IncarnamScan/output/npc-positions-wiki.json
--              + Ressources/Dofus/data/common/Npcs.d2o (dialog message IDs)
-- Idempotent : DELETE step removes any prior Incarnam rows for the
--              templates listed below (Preserved IDs are skipped).
--
-- Counts     : 55 npc_spawns
--              55 npc_actions
--              55 npc_replies
--
-- Farewell MessageId : 23053 ("Au revoir.")
--
-- Enum values are written numerically (DatabaseWriter convention,
-- cf. Sources/Giny.ORM/IO/DatabaseWriter.cs:203-210). Reader accepts
-- both name and numeric, so backward-compatible.
--   NpcActionsEnum.TALK            = 3
--   DirectionsEnum.DIRECTION_SOUTH = 2
--   GenericActionEnum.None         = 0
--
-- map override               : [2205] Mériana : 122683905 → 154010883 (Doflex coord was off-zone)
-- map override               : [2270] Flamme du Dark Vlad : 122683905 → 154010883 (Doflex coord was off-zone)
-- preserved (already in DB)  : [2897] Ganymède
-- skipped (not a dialogue)   : [4398] Portail vers Astrub
-- ============================================================

START TRANSACTION;

-- ============================================================
-- Section A : idempotent DELETE (children before parents,
--             preserved templates are never touched)
-- ============================================================

CREATE TEMPORARY TABLE _incarnam_spawn_ids (Id BIGINT NOT NULL PRIMARY KEY);

INSERT INTO _incarnam_spawn_ids (Id)
SELECT Id FROM npc_spawns
WHERE TemplateId IN (862, 871, 890, 1223, 1515, 2205, 2207, 2270, 2880, 2881, 2882, 2885, 2886, 2887, 2888, 2890, 2891, 2894, 2895, 2896, 2899, 2900, 2902, 2903, 2904, 2905, 2906, 2908, 2909, 2910, 2913, 2914, 2915, 2916, 2917, 2918, 2919, 2920, 2921, 2922, 2936, 2939, 2940, 2941, 2942, 2943, 2944, 2945, 2947, 2948, 2949, 2950, 3687, 5336, 7102)
  AND TemplateId NOT IN (2892, 2897);

DELETE FROM npc_replies WHERE NpcSpawnId IN (SELECT Id FROM _incarnam_spawn_ids);
DELETE FROM npc_actions WHERE NpcSpawnId IN (SELECT Id FROM _incarnam_spawn_ids);
DELETE FROM npc_spawns  WHERE Id          IN (SELECT Id FROM _incarnam_spawn_ids);

DROP TEMPORARY TABLE _incarnam_spawn_ids;

-- ============================================================
-- Section B : INSERTs (Id, NpcSpawnId, ReplyId all in 200_000+ range)
-- ============================================================

-- [862] Milicien Kerubim Nybi  (source=doflex, subArea=Cimetière, coords=3,0)
INSERT INTO npc_spawns (Id, TemplateId, MapId, CellId, Direction) VALUES (200001, 862, 153880064, 280, 2);
INSERT INTO npc_actions (Id, NpcSpawnId, Action, Param1) VALUES (200001, 200001, 3, '3691');
INSERT INTO npc_replies (Id, ReplyId, NpcSpawnId, MessageId, ActionIdentifier) VALUES (200001, 200001, 200001, 23053, 0);

-- [871] Laura  (source=doflex, subArea=Champs, coords=-3,-6)
INSERT INTO npc_spawns (Id, TemplateId, MapId, CellId, Direction) VALUES (200002, 871, 154011398, 280, 2);
INSERT INTO npc_actions (Id, NpcSpawnId, Action, Param1) VALUES (200002, 200002, 3, '3726');
INSERT INTO npc_replies (Id, ReplyId, NpcSpawnId, MessageId, ActionIdentifier) VALUES (200002, 200002, 200002, 23053, 0);

-- [890] Habitué de la taverne  (source=doflex, subArea=Taverne, coords=0,-3)
INSERT INTO npc_spawns (Id, TemplateId, MapId, CellId, Direction) VALUES (200003, 890, 153357316, 280, 2);
INSERT INTO npc_actions (Id, NpcSpawnId, Action, Param1) VALUES (200003, 200003, 3, '3849');
INSERT INTO npc_replies (Id, ReplyId, NpcSpawnId, MessageId, ActionIdentifier) VALUES (200003, 200003, 200003, 23053, 0);

-- [1223] Fée Risette  (source=doflex, subArea=Route des âmes, coords=2,-3)
INSERT INTO npc_spawns (Id, TemplateId, MapId, CellId, Direction) VALUES (200004, 1223, 153356296, 280, 2);
INSERT INTO npc_actions (Id, NpcSpawnId, Action, Param1) VALUES (200004, 200004, 3, '5452');
INSERT INTO npc_replies (Id, ReplyId, NpcSpawnId, MessageId, ActionIdentifier) VALUES (200004, 200004, 200004, 23053, 0);

-- [1515] Fécaline la Sage  (source=doflex, subArea=Route des âmes, coords=2,-3)
INSERT INTO npc_spawns (Id, TemplateId, MapId, CellId, Direction) VALUES (200005, 1515, 153356296, 280, 2);
INSERT INTO npc_actions (Id, NpcSpawnId, Action, Param1) VALUES (200005, 200005, 3, '20915');
INSERT INTO npc_replies (Id, ReplyId, NpcSpawnId, MessageId, ActionIdentifier) VALUES (200005, 200005, 200005, 23053, 0);

-- [2205] Mériana  (source=doflex, subArea=Queue du Dragon, coords=2,3)
INSERT INTO npc_spawns (Id, TemplateId, MapId, CellId, Direction) VALUES (200006, 2205, 154010883, 280, 2);
INSERT INTO npc_actions (Id, NpcSpawnId, Action, Param1) VALUES (200006, 200006, 3, '16694');
INSERT INTO npc_replies (Id, ReplyId, NpcSpawnId, MessageId, ActionIdentifier) VALUES (200006, 200006, 200006, 23053, 0);

-- [2207] Orbalantyr  (source=fallback-spawn-map, subArea=n/a, coords=n/a)
INSERT INTO npc_spawns (Id, TemplateId, MapId, CellId, Direction) VALUES (200007, 2207, 154010883, 280, 2);
INSERT INTO npc_actions (Id, NpcSpawnId, Action, Param1) VALUES (200007, 200007, 3, '16908');
INSERT INTO npc_replies (Id, ReplyId, NpcSpawnId, MessageId, ActionIdentifier) VALUES (200007, 200007, 200007, 23053, 0);

-- [2270] Flamme du Dark Vlad  (source=doflex, subArea=Queue du Dragon, coords=2,3)
INSERT INTO npc_spawns (Id, TemplateId, MapId, CellId, Direction) VALUES (200008, 2270, 154010883, 280, 2);
INSERT INTO npc_actions (Id, NpcSpawnId, Action, Param1) VALUES (200008, 200008, 3, '16975');
INSERT INTO npc_replies (Id, ReplyId, NpcSpawnId, MessageId, ActionIdentifier) VALUES (200008, 200008, 200008, 23053, 0);

-- [2880] Capitaine des Kerubims  (source=doflex, subArea=Champs, coords=0,-4)
INSERT INTO npc_spawns (Id, TemplateId, MapId, CellId, Direction) VALUES (200009, 2880, 81527303, 280, 2);
INSERT INTO npc_actions (Id, NpcSpawnId, Action, Param1) VALUES (200009, 200009, 3, '20873');
INSERT INTO npc_replies (Id, ReplyId, NpcSpawnId, MessageId, ActionIdentifier) VALUES (200009, 200009, 200009, 23053, 0);

-- [2881] Pipelette  (source=doflex, subArea=Route des âmes, coords=2,-3)
INSERT INTO npc_spawns (Id, TemplateId, MapId, CellId, Direction) VALUES (200010, 2881, 153356296, 280, 2);
INSERT INTO npc_actions (Id, NpcSpawnId, Action, Param1) VALUES (200010, 200010, 3, '20983');
INSERT INTO npc_replies (Id, ReplyId, NpcSpawnId, MessageId, ActionIdentifier) VALUES (200010, 200010, 200010, 23053, 0);

-- [2882] Xélora Fistol  (source=doflex, subArea=Pâturages, coords=2,-5)
INSERT INTO npc_spawns (Id, TemplateId, MapId, CellId, Direction) VALUES (200011, 2882, 153879813, 280, 2);
INSERT INTO npc_actions (Id, NpcSpawnId, Action, Param1) VALUES (200011, 200011, 3, '20856');
INSERT INTO npc_replies (Id, ReplyId, NpcSpawnId, MessageId, ActionIdentifier) VALUES (200011, 200011, 200011, 23053, 0);

-- [2885] Grobid  (source=doflex, subArea=Taverne, coords=0,-3)
INSERT INTO npc_spawns (Id, TemplateId, MapId, CellId, Direction) VALUES (200012, 2885, 153357316, 280, 2);
INSERT INTO npc_actions (Id, NpcSpawnId, Action, Param1) VALUES (200012, 200012, 3, '20864');
INSERT INTO npc_replies (Id, ReplyId, NpcSpawnId, MessageId, ActionIdentifier) VALUES (200012, 200012, 200012, 23053, 0);

-- [2886] Anta Brok  (source=doflex, subArea=Forêt, coords=1,-2)
INSERT INTO npc_spawns (Id, TemplateId, MapId, CellId, Direction) VALUES (200013, 2886, 153354248, 280, 2);
INSERT INTO npc_actions (Id, NpcSpawnId, Action, Param1) VALUES (200013, 200013, 3, '20974');
INSERT INTO npc_replies (Id, ReplyId, NpcSpawnId, MessageId, ActionIdentifier) VALUES (200013, 200013, 200013, 23053, 0);

-- [2887] Matu Vuh  (source=doflex, subArea=Champs, coords=-1,-6)
INSERT INTO npc_spawns (Id, TemplateId, MapId, CellId, Direction) VALUES (200014, 2887, 154010374, 280, 2);
INSERT INTO npc_actions (Id, NpcSpawnId, Action, Param1) VALUES (200014, 200014, 3, '20821');
INSERT INTO npc_replies (Id, ReplyId, NpcSpawnId, MessageId, ActionIdentifier) VALUES (200014, 200014, 200014, 23053, 0);

-- [2888] Marylork  (source=doflex, subArea=Pâturages, coords=3,-5)
INSERT INTO npc_spawns (Id, TemplateId, MapId, CellId, Direction) VALUES (200015, 2888, 153880325, 280, 2);
INSERT INTO npc_actions (Id, NpcSpawnId, Action, Param1) VALUES (200015, 200015, 3, '21196');
INSERT INTO npc_replies (Id, ReplyId, NpcSpawnId, MessageId, ActionIdentifier) VALUES (200015, 200015, 200015, 23053, 0);

-- [2890] Kandie la serveuse  (source=doflex, subArea=Taverne, coords=0,-3)
INSERT INTO npc_spawns (Id, TemplateId, MapId, CellId, Direction) VALUES (200016, 2890, 153357316, 280, 2);
INSERT INTO npc_actions (Id, NpcSpawnId, Action, Param1) VALUES (200016, 200016, 3, '21000');
INSERT INTO npc_replies (Id, ReplyId, NpcSpawnId, MessageId, ActionIdentifier) VALUES (200016, 200016, 200016, 23053, 0);

-- [2891] Pêcheur Gobelin  (source=doflex, subArea=Lac, coords=-1,-1)
INSERT INTO npc_spawns (Id, TemplateId, MapId, CellId, Direction) VALUES (200017, 2891, 154010369, 280, 2);
INSERT INTO npc_actions (Id, NpcSpawnId, Action, Param1) VALUES (200017, 200017, 3, '21753');
INSERT INTO npc_replies (Id, ReplyId, NpcSpawnId, MessageId, ActionIdentifier) VALUES (200017, 200017, 200017, 23053, 0);

-- [2894] Caporale Mynerve  (source=doflex, subArea=Champs, coords=0,-4)
INSERT INTO npc_spawns (Id, TemplateId, MapId, CellId, Direction) VALUES (200018, 2894, 81527303, 280, 2);
INSERT INTO npc_actions (Id, NpcSpawnId, Action, Param1) VALUES (200018, 200018, 3, '20905');
INSERT INTO npc_replies (Id, ReplyId, NpcSpawnId, MessageId, ActionIdentifier) VALUES (200018, 200018, 200018, 23053, 0);

-- [2895] Maître Hoboulo  (source=fallback-spawn-map, subArea=n/a, coords=n/a)
INSERT INTO npc_spawns (Id, TemplateId, MapId, CellId, Direction) VALUES (200019, 2895, 154010883, 280, 2);
INSERT INTO npc_actions (Id, NpcSpawnId, Action, Param1) VALUES (200019, 200019, 3, '20676');
INSERT INTO npc_replies (Id, ReplyId, NpcSpawnId, MessageId, ActionIdentifier) VALUES (200019, 200019, 200019, 23053, 0);

-- [2896] Maître Darm  (source=fallback-spawn-map, subArea=n/a, coords=n/a)
INSERT INTO npc_spawns (Id, TemplateId, MapId, CellId, Direction) VALUES (200020, 2896, 154010883, 280, 2);
INSERT INTO npc_actions (Id, NpcSpawnId, Action, Param1) VALUES (200020, 200020, 3, '20697');
INSERT INTO npc_replies (Id, ReplyId, NpcSpawnId, MessageId, ActionIdentifier) VALUES (200020, 200020, 200020, 23053, 0);

-- [2899] Berb Nhin  (source=doflex, subArea=Route des âmes, coords=0,-3)
INSERT INTO npc_spawns (Id, TemplateId, MapId, CellId, Direction) VALUES (200021, 2899, 153878787, 280, 2);
INSERT INTO npc_actions (Id, NpcSpawnId, Action, Param1) VALUES (200021, 200021, 3, '20861');
INSERT INTO npc_replies (Id, ReplyId, NpcSpawnId, MessageId, ActionIdentifier) VALUES (200021, 200021, 200021, 23053, 0);

-- [2900] Gérant de l''hôtel de vente  (source=doflex, subArea=Route des âmes, coords=1,-3)
INSERT INTO npc_spawns (Id, TemplateId, MapId, CellId, Direction) VALUES (200022, 2900, 153879299, 280, 2);
INSERT INTO npc_actions (Id, NpcSpawnId, Action, Param1) VALUES (200022, 200022, 3, '20759');
INSERT INTO npc_replies (Id, ReplyId, NpcSpawnId, MessageId, ActionIdentifier) VALUES (200022, 200022, 200022, 23053, 0);

-- [2902] Milicienne Kerubim  (source=doflex, subArea=Champs, coords=0,-4)
INSERT INTO npc_spawns (Id, TemplateId, MapId, CellId, Direction) VALUES (200023, 2902, 81527303, 280, 2);
INSERT INTO npc_actions (Id, NpcSpawnId, Action, Param1) VALUES (200023, 200023, 3, '21004');
INSERT INTO npc_replies (Id, ReplyId, NpcSpawnId, MessageId, ActionIdentifier) VALUES (200023, 200023, 200023, 23053, 0);

-- [2903] Echtelion  (source=doflex, subArea=Forêt, coords=1,-1)
INSERT INTO npc_spawns (Id, TemplateId, MapId, CellId, Direction) VALUES (200024, 2903, 153879297, 280, 2);
INSERT INTO npc_actions (Id, NpcSpawnId, Action, Param1) VALUES (200024, 200024, 3, '21003');
INSERT INTO npc_replies (Id, ReplyId, NpcSpawnId, MessageId, ActionIdentifier) VALUES (200024, 200024, 200024, 23053, 0);

-- [2904] Oskar Khas  (source=fallback-spawn-map, subArea=n/a, coords=n/a)
INSERT INTO npc_spawns (Id, TemplateId, MapId, CellId, Direction) VALUES (200025, 2904, 154010883, 280, 2);
INSERT INTO npc_actions (Id, NpcSpawnId, Action, Param1) VALUES (200025, 200025, 3, '20946');
INSERT INTO npc_replies (Id, ReplyId, NpcSpawnId, MessageId, ActionIdentifier) VALUES (200025, 200025, 200025, 23053, 0);

-- [2905] Ternette Nhin  (source=doflex, subArea=Route des âmes, coords=-1,-3)
INSERT INTO npc_spawns (Id, TemplateId, MapId, CellId, Direction) VALUES (200026, 2905, 154010371, 280, 2);
INSERT INTO npc_actions (Id, NpcSpawnId, Action, Param1) VALUES (200026, 200026, 3, '20795');
INSERT INTO npc_replies (Id, ReplyId, NpcSpawnId, MessageId, ActionIdentifier) VALUES (200026, 200026, 200026, 23053, 0);

-- [2906] Galilea  (source=doflex, subArea=Lac, coords=-1,1)
INSERT INTO npc_spawns (Id, TemplateId, MapId, CellId, Direction) VALUES (200027, 2906, 154010113, 280, 2);
INSERT INTO npc_actions (Id, NpcSpawnId, Action, Param1) VALUES (200027, 200027, 3, '20826');
INSERT INTO npc_replies (Id, ReplyId, NpcSpawnId, MessageId, ActionIdentifier) VALUES (200027, 200027, 200027, 23053, 0);

-- [2908] Kruella Freuz  (source=doflex, subArea=Forêt, coords=1,0)
INSERT INTO npc_spawns (Id, TemplateId, MapId, CellId, Direction) VALUES (200028, 2908, 153879040, 280, 2);
INSERT INTO npc_actions (Id, NpcSpawnId, Action, Param1) VALUES (200028, 200028, 3, '20900');
INSERT INTO npc_replies (Id, ReplyId, NpcSpawnId, MessageId, ActionIdentifier) VALUES (200028, 200028, 200028, 23053, 0);

-- [2909] Aléha  (source=doflex, subArea=Taverne, coords=0,-3)
INSERT INTO npc_spawns (Id, TemplateId, MapId, CellId, Direction) VALUES (200029, 2909, 153357316, 280, 2);
INSERT INTO npc_actions (Id, NpcSpawnId, Action, Param1) VALUES (200029, 200029, 3, '20931');
INSERT INTO npc_replies (Id, ReplyId, NpcSpawnId, MessageId, ActionIdentifier) VALUES (200029, 200029, 200029, 23053, 0);

-- [2910] Hollie Brok  (source=doflex, subArea=Route des âmes, coords=1,-3)
INSERT INTO npc_spawns (Id, TemplateId, MapId, CellId, Direction) VALUES (200030, 2910, 153879299, 280, 2);
INSERT INTO npc_actions (Id, NpcSpawnId, Action, Param1) VALUES (200030, 200030, 3, '20910');
INSERT INTO npc_replies (Id, ReplyId, NpcSpawnId, MessageId, ActionIdentifier) VALUES (200030, 200030, 200030, 23053, 0);

-- [2913] Niko la Flammèche  (source=doflex, subArea=Lac, coords=-2,-2)
INSERT INTO npc_spawns (Id, TemplateId, MapId, CellId, Direction) VALUES (200031, 2913, 153355270, 280, 2);
INSERT INTO npc_actions (Id, NpcSpawnId, Action, Param1) VALUES (200031, 200031, 3, '21111');
INSERT INTO npc_replies (Id, ReplyId, NpcSpawnId, MessageId, ActionIdentifier) VALUES (200031, 200031, 200031, 23053, 0);

-- [2914] Pyracelse  (source=doflex, subArea=Lac, coords=-2,-2)
INSERT INTO npc_spawns (Id, TemplateId, MapId, CellId, Direction) VALUES (200032, 2914, 153355270, 280, 2);
INSERT INTO npc_actions (Id, NpcSpawnId, Action, Param1) VALUES (200032, 200032, 3, '21114');
INSERT INTO npc_replies (Id, ReplyId, NpcSpawnId, MessageId, ActionIdentifier) VALUES (200032, 200032, 200032, 23053, 0);

-- [2915] Pat Hapin  (source=doflex, subArea=Champs, coords=-1,-4)
INSERT INTO npc_spawns (Id, TemplateId, MapId, CellId, Direction) VALUES (200033, 2915, 153354242, 280, 2);
INSERT INTO npc_actions (Id, NpcSpawnId, Action, Param1) VALUES (200033, 200033, 3, '21108');
INSERT INTO npc_replies (Id, ReplyId, NpcSpawnId, MessageId, ActionIdentifier) VALUES (200033, 200033, 200033, 23053, 0);

-- [2916] Lucie Lure  (source=doflex, subArea=Lac, coords=0,-1)
INSERT INTO npc_spawns (Id, TemplateId, MapId, CellId, Direction) VALUES (200034, 2916, 153354246, 280, 2);
INSERT INTO npc_actions (Id, NpcSpawnId, Action, Param1) VALUES (200034, 200034, 3, '21104');
INSERT INTO npc_replies (Id, ReplyId, NpcSpawnId, MessageId, ActionIdentifier) VALUES (200034, 200034, 200034, 23053, 0);

-- [2917] Nina Chichi  (source=doflex, subArea=Lac, coords=0,-2)
INSERT INTO npc_spawns (Id, TemplateId, MapId, CellId, Direction) VALUES (200035, 2917, 153354244, 280, 2);
INSERT INTO npc_actions (Id, NpcSpawnId, Action, Param1) VALUES (200035, 200035, 3, '20992');
INSERT INTO npc_replies (Id, ReplyId, NpcSpawnId, MessageId, ActionIdentifier) VALUES (200035, 200035, 200035, 23053, 0);

-- [2918] Rodrigo Dillo  (source=doflex, subArea=Lac, coords=0,-2)
INSERT INTO npc_spawns (Id, TemplateId, MapId, CellId, Direction) VALUES (200036, 2918, 153354244, 280, 2);
INSERT INTO npc_actions (Id, NpcSpawnId, Action, Param1) VALUES (200036, 200036, 3, '21101');
INSERT INTO npc_replies (Id, ReplyId, NpcSpawnId, MessageId, ActionIdentifier) VALUES (200036, 200036, 200036, 23053, 0);

-- [2919] Carla Fabrégé  (source=doflex, subArea=Pâturages, coords=1,-4)
INSERT INTO npc_spawns (Id, TemplateId, MapId, CellId, Direction) VALUES (200037, 2919, 153355272, 280, 2);
INSERT INTO npc_actions (Id, NpcSpawnId, Action, Param1) VALUES (200037, 200037, 3, '21098');
INSERT INTO npc_replies (Id, ReplyId, NpcSpawnId, MessageId, ActionIdentifier) VALUES (200037, 200037, 200037, 23053, 0);

-- [2920] Benarde Pivote  (source=doflex, subArea=Forêt, coords=1,-2)
INSERT INTO npc_spawns (Id, TemplateId, MapId, CellId, Direction) VALUES (200038, 2920, 153354248, 280, 2);
INSERT INTO npc_actions (Id, NpcSpawnId, Action, Param1) VALUES (200038, 200038, 3, '21095');
INSERT INTO npc_replies (Id, ReplyId, NpcSpawnId, MessageId, ActionIdentifier) VALUES (200038, 200038, 200038, 23053, 0);

-- [2921] Joseph Ahistos  (source=doflex, subArea=Pâturages, coords=2,-4)
INSERT INTO npc_spawns (Id, TemplateId, MapId, CellId, Direction) VALUES (200039, 2921, 153355264, 280, 2);
INSERT INTO npc_actions (Id, NpcSpawnId, Action, Param1) VALUES (200039, 200039, 3, '21090');
INSERT INTO npc_replies (Id, ReplyId, NpcSpawnId, MessageId, ActionIdentifier) VALUES (200039, 200039, 200039, 23053, 0);

-- [2922] Ramille Clodel  (source=doflex, subArea=Forêt, coords=2,-2)
INSERT INTO npc_spawns (Id, TemplateId, MapId, CellId, Direction) VALUES (200040, 2922, 153355266, 280, 2);
INSERT INTO npc_actions (Id, NpcSpawnId, Action, Param1) VALUES (200040, 200040, 3, '21085');
INSERT INTO npc_replies (Id, ReplyId, NpcSpawnId, MessageId, ActionIdentifier) VALUES (200040, 200040, 200040, 23053, 0);

-- [2936] Kardorim  (source=fallback-spawn-map, subArea=n/a, coords=n/a)
INSERT INTO npc_spawns (Id, TemplateId, MapId, CellId, Direction) VALUES (200041, 2936, 154010883, 280, 2);
INSERT INTO npc_actions (Id, NpcSpawnId, Action, Param1) VALUES (200041, 200041, 3, '20842');
INSERT INTO npc_replies (Id, ReplyId, NpcSpawnId, MessageId, ActionIdentifier) VALUES (200041, 200041, 200041, 23053, 0);

-- [2939] Henri Daul  (source=doflex, subArea=Route des âmes, coords=3,-3)
INSERT INTO npc_spawns (Id, TemplateId, MapId, CellId, Direction) VALUES (200042, 2939, 153354240, 280, 2);
INSERT INTO npc_actions (Id, NpcSpawnId, Action, Param1) VALUES (200042, 200042, 3, '21077');
INSERT INTO npc_replies (Id, ReplyId, NpcSpawnId, MessageId, ActionIdentifier) VALUES (200042, 200042, 200042, 23053, 0);

-- [2940] Jonquille  (source=doflex, subArea=Route des âmes, coords=2,-3)
INSERT INTO npc_spawns (Id, TemplateId, MapId, CellId, Direction) VALUES (200043, 2940, 153356296, 280, 2);
INSERT INTO npc_actions (Id, NpcSpawnId, Action, Param1) VALUES (200043, 200043, 3, '20987');
INSERT INTO npc_replies (Id, ReplyId, NpcSpawnId, MessageId, ActionIdentifier) VALUES (200043, 200043, 200043, 23053, 0);

-- [2941] Rebecca Risseuz  (source=doflex, subArea=Lac, coords=-1,-2)
INSERT INTO npc_spawns (Id, TemplateId, MapId, CellId, Direction) VALUES (200044, 2941, 153355268, 280, 2);
INSERT INTO npc_actions (Id, NpcSpawnId, Action, Param1) VALUES (200044, 200044, 3, '21070');
INSERT INTO npc_replies (Id, ReplyId, NpcSpawnId, MessageId, ActionIdentifier) VALUES (200044, 200044, 200044, 23053, 0);

-- [2942] André Rieur  (source=doflex, subArea=Taverne, coords=0,-3)
INSERT INTO npc_spawns (Id, TemplateId, MapId, CellId, Direction) VALUES (200045, 2942, 153357316, 280, 2);
INSERT INTO npc_actions (Id, NpcSpawnId, Action, Param1) VALUES (200045, 200045, 3, '20945');
INSERT INTO npc_replies (Id, ReplyId, NpcSpawnId, MessageId, ActionIdentifier) VALUES (200045, 200045, 200045, 23053, 0);

-- [2943] Alexandra Godinda  (source=doflex, subArea=Champs, coords=-1,-5)
INSERT INTO npc_spawns (Id, TemplateId, MapId, CellId, Direction) VALUES (200046, 2943, 154010373, 280, 2);
INSERT INTO npc_actions (Id, NpcSpawnId, Action, Param1) VALUES (200046, 200046, 3, '21029');
INSERT INTO npc_replies (Id, ReplyId, NpcSpawnId, MessageId, ActionIdentifier) VALUES (200046, 200046, 200046, 23053, 0);

-- [2944] Tornazo  (source=doflex, subArea=Champs, coords=-1,-5)
INSERT INTO npc_spawns (Id, TemplateId, MapId, CellId, Direction) VALUES (200047, 2944, 154010373, 280, 2);
INSERT INTO npc_actions (Id, NpcSpawnId, Action, Param1) VALUES (200047, 200047, 3, '21025');
INSERT INTO npc_replies (Id, ReplyId, NpcSpawnId, MessageId, ActionIdentifier) VALUES (200047, 200047, 200047, 23053, 0);

-- [2945] Gardien de l''Orme  (source=doflex, subArea=Lac, coords=0,1)
INSERT INTO npc_spawns (Id, TemplateId, MapId, CellId, Direction) VALUES (200048, 2945, 153878529, 280, 2);
INSERT INTO npc_actions (Id, NpcSpawnId, Action, Param1) VALUES (200048, 200048, 3, '20990');
INSERT INTO npc_replies (Id, ReplyId, NpcSpawnId, MessageId, ActionIdentifier) VALUES (200048, 200048, 200048, 23053, 0);

-- [2947] Punition  (source=doflex, subArea=Champs, coords=-1,-5)
INSERT INTO npc_spawns (Id, TemplateId, MapId, CellId, Direction) VALUES (200049, 2947, 154010373, 280, 2);
INSERT INTO npc_actions (Id, NpcSpawnId, Action, Param1) VALUES (200049, 200049, 3, '21026');
INSERT INTO npc_replies (Id, ReplyId, NpcSpawnId, MessageId, ActionIdentifier) VALUES (200049, 200049, 200049, 23053, 0);

-- [2948] Silvin  (source=doflex, subArea=Champs, coords=-1,-5)
INSERT INTO npc_spawns (Id, TemplateId, MapId, CellId, Direction) VALUES (200050, 2948, 154010373, 280, 2);
INSERT INTO npc_actions (Id, NpcSpawnId, Action, Param1) VALUES (200050, 200050, 3, '21027');
INSERT INTO npc_replies (Id, ReplyId, NpcSpawnId, MessageId, ActionIdentifier) VALUES (200050, 200050, 200050, 23053, 0);

-- [2949] Silvette  (source=doflex, subArea=Champs, coords=-1,-5)
INSERT INTO npc_spawns (Id, TemplateId, MapId, CellId, Direction) VALUES (200051, 2949, 154010373, 280, 2);
INSERT INTO npc_actions (Id, NpcSpawnId, Action, Param1) VALUES (200051, 200051, 3, '21028');
INSERT INTO npc_replies (Id, ReplyId, NpcSpawnId, MessageId, ActionIdentifier) VALUES (200051, 200051, 200051, 23053, 0);

-- [2950] Baffeur  (source=doflex, subArea=Champs, coords=-1,-5)
INSERT INTO npc_spawns (Id, TemplateId, MapId, CellId, Direction) VALUES (200052, 2950, 154010373, 280, 2);
INSERT INTO npc_actions (Id, NpcSpawnId, Action, Param1) VALUES (200052, 200052, 3, '21024');
INSERT INTO npc_replies (Id, ReplyId, NpcSpawnId, MessageId, ActionIdentifier) VALUES (200052, 200052, 200052, 23053, 0);

-- [3687] Orbalantyr de Mériana  (source=fallback-spawn-map, subArea=Marécages nauséabonds, coords=-6,-3)
INSERT INTO npc_spawns (Id, TemplateId, MapId, CellId, Direction) VALUES (200053, 3687, 154010883, 280, 2);
INSERT INTO npc_actions (Id, NpcSpawnId, Action, Param1) VALUES (200053, 200053, 3, '26449');
INSERT INTO npc_replies (Id, ReplyId, NpcSpawnId, MessageId, ActionIdentifier) VALUES (200053, 200053, 200053, 23053, 0);

-- [5336] Goultard  (source=fallback-spawn-map, subArea=Arènes de Goultard, coords=-6,-12)
INSERT INTO npc_spawns (Id, TemplateId, MapId, CellId, Direction) VALUES (200054, 5336, 154010883, 280, 2);
INSERT INTO npc_actions (Id, NpcSpawnId, Action, Param1) VALUES (200054, 200054, 3, '39150');
INSERT INTO npc_replies (Id, ReplyId, NpcSpawnId, MessageId, ActionIdentifier) VALUES (200054, 200054, 200054, 23053, 0);

-- [7102] Andrée Inkarnay  (source=fallback-spawn-map, subArea=n/a, coords=n/a)
INSERT INTO npc_spawns (Id, TemplateId, MapId, CellId, Direction) VALUES (200055, 7102, 154010883, 280, 2);
INSERT INTO npc_actions (Id, NpcSpawnId, Action, Param1) VALUES (200055, 200055, 3, '52780');
INSERT INTO npc_replies (Id, ReplyId, NpcSpawnId, MessageId, ActionIdentifier) VALUES (200055, 200055, 200055, 23053, 0);

-- ============================================================
COMMIT;

-- Done. Restart the world server (or call NpcSpawnRecord.Initialize)
-- to load the new rows into memory.
