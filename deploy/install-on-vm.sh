#!/bin/bash
# Installation initiale des services Deindeiru sur la VM cloud.
# À exécuter UNE SEULE FOIS, AVANT le premier deploy.ps1.
#
# Procédure :
#   1. Copier ce dossier deploy/ sur la VM, ex. depuis le poste Windows :
#        scp -r deploy deindeiru@deindeiruworld.duckdns.org:/tmp/
#   2. Sur la VM :
#        bash /tmp/deploy/install-on-vm.sh
#
# Le script crée l'arborescence /opt/deindeiru, installe et active les
# services systemd. Il NE démarre PAS les serveurs : les binaires et les
# fichiers config.Production.json ne sont pas encore en place à ce stade.

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "==> Création de l'arborescence /opt/deindeiru ..."
sudo mkdir -p /opt/deindeiru/auth \
              /opt/deindeiru/world \
              /opt/deindeiru/logs \
              /opt/deindeiru/backups
sudo chown -R deindeiru:deindeiru /opt/deindeiru

echo "==> Installation des unités systemd ..."
sudo cp "$SCRIPT_DIR/systemd/deindeiru-auth.service"  /etc/systemd/system/
sudo cp "$SCRIPT_DIR/systemd/deindeiru-world.service" /etc/systemd/system/

sudo systemctl daemon-reload
sudo systemctl enable deindeiru-auth deindeiru-world

echo ""
echo "Services installés et activés (démarrage auto au boot)."
echo ""
echo "Étapes suivantes :"
echo "  1. Depuis le poste Windows :  cd deploy ; .\\deploy.ps1"
echo "     (envoie les binaires ; les services ne démarreront pas encore)"
echo "  2. Sur la VM, créer les configs de production :"
echo "       cp /opt/deindeiru/auth/config.Production.example.json  /opt/deindeiru/auth/config.Production.json"
echo "       cp /opt/deindeiru/world/config.Production.example.json /opt/deindeiru/world/config.Production.json"
echo "       nano /opt/deindeiru/auth/config.Production.json    # renseigner SQLPassword"
echo "       nano /opt/deindeiru/world/config.Production.json   # renseigner SQLPassword"
echo "  3. Relancer .\\deploy.ps1 depuis Windows (démarre les services)."
