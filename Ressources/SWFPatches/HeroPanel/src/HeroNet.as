package
{
   import flash.utils.getDefinitionByName;

   /// Envoi client → serveur. Le panneau héros pilote le HeroGroup via les
   /// commandes chat ".hero*" déjà gérées côté serveur (HeroCommands). On
   /// construit donc un ChatClientMultiMessage et on le pousse dans la
   /// connexion courante — aucune nouvelle classe protocole côté client.
   public class HeroNet
   {
      public function HeroNet()
      {
      }

      /// Envoie une commande chat (sans le point initial : "herocreate",
      /// "heroadd Bob"...). Le serveur préfixe par "." pour router.
      public static function command(verb:String):void
      {
         try
         {
            var connHandler:* = getDefinitionByName(
               "com.ankamagames.dofus.kernel.net::ConnectionsHandler");

            var connection:* = connHandler.getConnection();

            if (connection == null)
            {
               return;
            }

            var msgClass:Class = getDefinitionByName(
               "com.ankamagames.dofus.network.messages.game.chat::ChatClientMultiMessage")
               as Class;

            var msg:* = new msgClass();
            msg.initChatClientMultiMessage("." + verb, 0);
            connection.send(msg);
         }
         catch (e:Error)
         {
         }
      }
   }
}
