package
{
   import flash.display.Sprite;
   import flash.events.MouseEvent;
   import flash.text.TextField;
   import flash.text.TextFieldAutoSize;
   import flash.text.TextFormat;

   /// Panneau "Mode Héros" — UI Flash autonome (aucune dépendance Berilia),
   /// ajoutée directement sur la stage du client. Affiche le HeroGroup et
   /// expose toutes les actions (créer / ajouter / retirer / chef / jouer)
   /// sous forme de boutons qui envoient les commandes ".hero*" au serveur.
   public class HeroPanel extends Sprite
   {
      private static const WIN_W:int = 380;
      private static const TITLE_H:int = 28;

      private static const C_WIN_BG:uint = 0x21242b;
      private static const C_TITLE:uint = 0x2e3138;
      private static const C_BORDER:uint = 0x12141a;
      private static const C_GOLD:uint = 0xf2b13c;
      private static const C_TEXT:uint = 0xe6e6e6;
      private static const C_DIM:uint = 0x8b8f98;
      private static const C_GREEN:uint = 0x7ac459;
      private static const C_BTN:uint = 0x3c424c;
      private static const C_BTN_HOVER:uint = 0x515a68;
      private static const C_BTN_DOWN:uint = 0x2b2e34;

      private var _stage:*;
      private var _window:Sprite;
      private var _titleBar:Sprite;
      private var _content:Sprite;
      private var _data:Object;

      public function HeroPanel()
      {
         super();
         buildToggle();
         buildWindow();
      }

      /// Ancre le panneau à la stage et installe le suivi de drag.
      public function attach(stage:*):void
      {
         _stage = stage;

         try
         {
            _stage.addEventListener(MouseEvent.MOUSE_UP, function(e:MouseEvent):void
            {
               try { _window.stopDrag(); } catch (err:Error) {}
            });
         }
         catch (e:Error)
         {
         }
      }

      /// Callback de HeroFrame : nouvel état du groupe (JSON) reçu du serveur.
      public function onState(json:String):void
      {
         try
         {
            _data = JSON.parse(json);
         }
         catch (e:Error)
         {
            _data = null;
         }

         if (_window.visible)
         {
            rebuild();
         }
      }

      // ---------------------------------------------------------------
      //  Bouton d'ouverture (toujours visible)
      // ---------------------------------------------------------------

      private function buildToggle():void
      {
         var toggle:Sprite = makeButton("Héros", 70, true, function():void
         {
            setOpen(!_window.visible);
         });
         toggle.x = 6;
         toggle.y = 250;
         addChild(toggle);
      }

      private function setOpen(open:Boolean):void
      {
         _window.visible = open;

         if (open)
         {
            // Demande un état frais à chaque ouverture.
            HeroNet.command("herostate");
            rebuild();
         }
      }

      // ---------------------------------------------------------------
      //  Fenêtre
      // ---------------------------------------------------------------

      private function buildWindow():void
      {
         _window = new Sprite();
         _window.x = 86;
         _window.y = 120;
         _window.visible = false;
         addChild(_window);

         _titleBar = new Sprite();
         _titleBar.buttonMode = true;
         _titleBar.addEventListener(MouseEvent.MOUSE_DOWN, function(e:MouseEvent):void
         {
            try { _window.startDrag(); } catch (err:Error) {}
         });
         _window.addChild(_titleBar);

         var title:TextField = makeLabel("Mode Héros", 13, C_GOLD, true);
         title.x = 12;
         title.y = 6;
         _titleBar.addChild(title);

         var close:Sprite = makeButton("X", 22, true, function():void
         {
            setOpen(false);
         });
         close.x = WIN_W - 12 - 22;
         close.y = 3;
         _titleBar.addChild(close);

         _content = new Sprite();
         _content.y = TITLE_H;
         _window.addChild(_content);

         drawWindow(220);
      }

      private function drawWindow(height:int):void
      {
         _window.graphics.clear();
         _window.graphics.lineStyle(1, C_BORDER);
         _window.graphics.beginFill(C_WIN_BG);
         _window.graphics.drawRoundRect(0, 0, WIN_W, height, 10, 10);
         _window.graphics.endFill();

         _titleBar.graphics.clear();
         _titleBar.graphics.beginFill(C_TITLE);
         _titleBar.graphics.drawRoundRect(0, 0, WIN_W, TITLE_H, 10, 10);
         _titleBar.graphics.drawRect(0, TITLE_H - 10, WIN_W, 10);
         _titleBar.graphics.endFill();
      }

      // ---------------------------------------------------------------
      //  Reconstruction du contenu
      // ---------------------------------------------------------------

      private function rebuild():void
      {
         while (_content.numChildren > 0)
         {
            _content.removeChildAt(0);
         }

         var y:int = 10;

         if (_data == null)
         {
            addText(_content, "Chargement…", 12, y, C_DIM, false);
            drawWindow(TITLE_H + y + 30);
            return;
         }

         var iAmLeader:Boolean = Number(_data.active) == Number(_data.leader);
         var hasGroup:Boolean = Number(_data.hg) != -1;
         var fighting:Boolean = _data.fight == true;
         var members:Array = (_data.members is Array) ? _data.members : [];
         var avail:Array = (_data.avail is Array) ? _data.avail : [];
         var max:int = int(_data.max);

         // --- Section groupe ---
         addText(_content,
            hasGroup ? ("Groupe — " + members.length + "/" + max) : "Aucun groupe de héros",
            12, y, C_GOLD, true);
         y += 23;

         if (!hasGroup)
         {
            var createBtn:Sprite = makeButton("Créer un groupe de héros", 220, !fighting,
               function():void { HeroNet.command("herocreate"); });
            createBtn.x = 12;
            createBtn.y = y;
            _content.addChild(createBtn);
            y += 34;
         }
         else
         {
            for (var i:int = 0; i < members.length; i++)
            {
               var row:Sprite = buildMemberRow(members[i], iAmLeader, fighting);
               row.y = y;
               _content.addChild(row);
               y += 32;
            }
         }

         y += 8;

         // --- Section personnages disponibles ---
         addText(_content, "Personnages disponibles", 12, y, C_GOLD, true);
         y += 23;

         if (avail.length == 0)
         {
            addText(_content, "Aucun autre personnage sur le compte.", 12, y, C_DIM, false);
            y += 24;
         }
         else
         {
            var canAdd:Boolean = iAmLeader && hasGroup && members.length < max && !fighting;

            for (var j:int = 0; j < avail.length; j++)
            {
               var arow:Sprite = buildAvailRow(avail[j], canAdd);
               arow.y = y;
               _content.addChild(arow);
               y += 30;
            }
         }

         y += 10;

         if (fighting)
         {
            addText(_content, "Combat en cours — actions limitées.", 12, y, C_DIM, false);
            y += 20;
         }

         drawWindow(TITLE_H + y + 6);
      }

      private function buildMemberRow(m:Object, iAmLeader:Boolean, fighting:Boolean):Sprite
      {
         var row:Sprite = new Sprite();

         var isLeader:Boolean = Number(m.id) == Number(_data.leader);
         var isActive:Boolean = Number(m.id) == Number(_data.active);
         var heroName:String = String(m.n);

         var nameColor:uint = isLeader ? C_GOLD : (isActive ? C_GREEN : C_TEXT);
         var name:TextField = addText(row, heroName, 12, 4, nameColor, isLeader || isActive);

         var role:String = "";
         if (isLeader) role = "chef";
         if (isActive) role = (role.length > 0 ? role + ", actif" : "actif");
         if (role.length > 0)
         {
            addText(row, "(" + role + ")", 14 + name.width + 6, 6, C_DIM, false);
         }

         addText(row, "Niv " + m.lvl, 156, 6, C_DIM, false);

         // Jouer (switch) : possible si pas déjà actif et hors combat.
         var play:Sprite = makeButton("Jouer", 50, !isActive && !fighting,
            function():void { HeroNet.command("heroswitch " + heroName); });
         play.x = 204;
         row.addChild(play);

         // Chef (leader) : le leader courant peut transmettre le rôle.
         var chief:Sprite = makeButton("Chef", 46, iAmLeader && !isLeader,
            function():void { HeroNet.command("heroleader " + heroName); });
         chief.x = 260;
         row.addChild(chief);

         // Retirer : le leader peut retirer un membre (sauf lui-même / l'actif).
         var remove:Sprite = makeButton("Retirer", 56, iAmLeader && !isLeader && !isActive,
            function():void { HeroNet.command("heroremove " + heroName); });
         remove.x = 312;
         row.addChild(remove);

         return row;
      }

      private function buildAvailRow(c:Object, canAdd:Boolean):Sprite
      {
         var row:Sprite = new Sprite();
         var charName:String = String(c.n);

         addText(row, charName, 12, 4, C_TEXT, false);
         addText(row, "Niv " + c.lvl, 156, 5, C_DIM, false);

         var add:Sprite = makeButton("Ajouter", 64, canAdd,
            function():void { HeroNet.command("heroadd " + charName); });
         add.x = 304;
         row.addChild(add);

         return row;
      }

      // ---------------------------------------------------------------
      //  Fabriques UI
      // ---------------------------------------------------------------

      private function makeButton(label:String, w:int, enabled:Boolean, onClick:Function):Sprite
      {
         var btn:Sprite = new Sprite();
         var h:int = 22;

         drawButton(btn, w, h, enabled ? C_BTN : C_BTN_DOWN);

         var tf:TextField = makeLabel(label, 11, enabled ? C_TEXT : C_DIM, false);
         tf.autoSize = TextFieldAutoSize.NONE;
         tf.width = w;
         tf.height = h;
         tf.x = 0;
         tf.y = 4;
         var fmt:TextFormat = tf.defaultTextFormat;
         fmt.align = "center";
         tf.setTextFormat(fmt);
         btn.addChild(tf);

         if (enabled)
         {
            btn.buttonMode = true;
            btn.mouseChildren = false;
            btn.addEventListener(MouseEvent.MOUSE_OVER, function(e:MouseEvent):void
            {
               drawButton(btn, w, h, C_BTN_HOVER);
            });
            btn.addEventListener(MouseEvent.MOUSE_OUT, function(e:MouseEvent):void
            {
               drawButton(btn, w, h, C_BTN);
            });
            btn.addEventListener(MouseEvent.CLICK, function(e:MouseEvent):void
            {
               try { onClick(); } catch (err:Error) {}
            });
         }

         return btn;
      }

      private function drawButton(btn:Sprite, w:int, h:int, color:uint):void
      {
         btn.graphics.clear();
         btn.graphics.beginFill(color);
         btn.graphics.drawRoundRect(0, 0, w, h, 6, 6);
         btn.graphics.endFill();
      }

      private function makeLabel(text:String, size:int, color:uint, bold:Boolean):TextField
      {
         var tf:TextField = new TextField();
         tf.selectable = false;
         tf.mouseEnabled = false;
         tf.autoSize = TextFieldAutoSize.LEFT;
         tf.defaultTextFormat = new TextFormat("_sans", size, color, bold);
         tf.text = text;
         return tf;
      }

      private function addText(parent:Sprite, text:String, x:int, y:int,
         color:uint, bold:Boolean):TextField
      {
         var tf:TextField = makeLabel(text, 12, color, bold);
         tf.x = x;
         tf.y = y;
         parent.addChild(tf);
         return tf;
      }
   }
}
