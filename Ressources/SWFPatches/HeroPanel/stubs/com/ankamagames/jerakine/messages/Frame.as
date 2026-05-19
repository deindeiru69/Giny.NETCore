package com.ankamagames.jerakine.messages
{
   import com.ankamagames.jerakine.utils.misc.Prioritizable;

   // Stub — miroir exact de l'interface Frame du client Dofus. Permet à
   // HeroFrame d'implémenter le contrat attendu par Worker.addFrame(). À
   // l'exécution, la définition du client prévaut (domaine parent).
   public interface Frame extends MessageHandler, Prioritizable
   {
      function pushed() : Boolean;

      function pulled() : Boolean;
   }
}
