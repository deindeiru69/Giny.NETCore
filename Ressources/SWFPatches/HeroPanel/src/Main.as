package
{
   import flash.display.Sprite;
   import flash.events.TimerEvent;
   import flash.utils.Timer;
   import flash.utils.getDefinitionByName;
   import flash.utils.getQualifiedClassName;

   /// Point d'entrée du RawPatch "panneau héros". Le SWF est chargé par le
   /// client (ServerControlFrame) dès la connexion au World ; sa stage n'est
   /// pas encore prête. On attend donc activement que la stage de jeu soit
   /// disponible, puis on installe le panneau et la Frame réseau.
   public class Main extends Sprite
   {
      private var _timer:Timer;

      public function Main()
      {
         super();

         // Poll indéfini (600 ms) : le SWF est chargé dès la connexion au
         // World, bien avant que le client soit en jeu. On boote seulement
         // une fois la stage prête ET un perso effectivement en jeu, pour ne
         // pas afficher le panneau sur l'écran de sélection de personnage.
         _timer = new Timer(600, 0);
         _timer.addEventListener(TimerEvent.TIMER, onTick);
         _timer.start();
      }

      private function onTick(e:TimerEvent):void
      {
         var stage:* = getGameStage();

         if (stage == null || !isInGame())
         {
            return;
         }

         _timer.stop();
         _timer.removeEventListener(TimerEvent.TIMER, onTick);

         try
         {
            boot(stage);
         }
         catch (err:Error)
         {
         }
      }

      /// True quand un personnage est effectivement en jeu (id != 0).
      private function isInGame():Boolean
      {
         try
         {
            var pcm:* = getDefinitionByName(
               "com.ankamagames.dofus.logic.game.common.managers::PlayedCharacterManager")
               .getInstance();
            return pcm != null && Number(pcm.id) != 0;
         }
         catch (e:Error)
         {
         }
         return false;
      }

      private function getGameStage():*
      {
         try
         {
            var ssm:* = getDefinitionByName(
               "com.ankamagames.jerakine.utils.display::StageShareManager");
            return ssm.stage;
         }
         catch (e:Error)
         {
         }
         return null;
      }

      private function getWorker():*
      {
         try
         {
            var kernel:* = getDefinitionByName("com.ankamagames.dofus.kernel::Kernel");
            return kernel.getWorker();
         }
         catch (e:Error)
         {
         }
         return null;
      }

      private function boot(stage:*):void
      {
         // Reconnexion sans redémarrage du client : retirer l'ancien panneau.
         var previous:* = stage.getChildByName("ginyHeroPanel");
         if (previous != null)
         {
            try { stage.removeChild(previous); } catch (e:Error) {}
         }

         var worker:* = getWorker();

         // …et retirer les anciennes HeroFrame restées dans le Worker.
         if (worker != null)
         {
            try
            {
               var frames:* = worker.framesList;
               var i:int = int(frames.length) - 1;
               while (i >= 0)
               {
                  if (getQualifiedClassName(frames[i]) == "HeroFrame")
                  {
                     worker.removeFrame(frames[i]);
                  }
                  i--;
               }
            }
            catch (e:Error)
            {
            }
         }

         var panel:HeroPanel = new HeroPanel();
         panel.name = "ginyHeroPanel";
         stage.addChild(panel);
         panel.attach(stage);

         if (worker != null)
         {
            try
            {
               worker.addFrame(new HeroFrame(panel.onState));
            }
            catch (e:Error)
            {
            }
         }
      }
   }
}
