package
{
   import com.ankamagames.jerakine.messages.Frame;
   import com.ankamagames.jerakine.messages.Message;
   import flash.utils.getQualifiedClassName;

   /// Frame réseau injectée dans le Worker du client. Intercepte les
   /// TextInformationMessage "sentinelle" (msgType == STATE_MSG_TYPE) émis par
   /// le serveur pour transporter l'état du HeroGroup, et les consomme
   /// (process renvoie true) pour qu'ils n'apparaissent jamais dans le chat.
   /// Tous les autres messages passent (process renvoie false).
   public class HeroFrame implements Frame
   {
      /// Doit correspondre à HeroPanelManager.HeroStateMsgType (serveur).
      /// <= 127 : le client lit msgType via un readByte() signé.
      public static const STATE_MSG_TYPE:int = 99;

      private var _onState:Function;

      public function HeroFrame(onState:Function)
      {
         super();
         _onState = onState;
      }

      /// Priorité élevée : traité avant le ChatFrame (priorité 0), ce qui
      /// permet de consumer le message sentinelle avant tout affichage.
      public function get priority() : int
      {
         return 100;
      }

      public function pushed() : Boolean
      {
         return true;
      }

      public function pulled() : Boolean
      {
         return true;
      }

      public function process(msg:Message) : Boolean
      {
         // Identification par nom de classe : aucune dépendance à
         // getDefinitionByName ni à l'opérateur "is" (plus robuste).
         if (getQualifiedClassName(msg).indexOf("TextInformationMessage") == -1)
         {
            return false;
         }

         var info:* = msg;

         if (int(info.msgType) != STATE_MSG_TYPE)
         {
            return false;
         }

         try
         {
            var json:String = "";

            if (info.parameters != null && info.parameters.length > 0)
            {
               json = String(info.parameters[0]);
            }

            if (_onState != null)
            {
               _onState(json);
            }
         }
         catch (e:Error)
         {
         }

         // Message consommé : il ne poursuit pas vers le ChatFrame.
         return true;
      }
   }
}
