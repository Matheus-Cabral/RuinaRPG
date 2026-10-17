.PHONY: deploy up down clean clean-data logs migrate status rollback grant-rules-auditor revoke-rules-auditor reset-gm-password

# Caminho manual (emergência/local): o deploy normal é o pipeline (Técnico R0013-R0015).
# deploy.sh builda as imagens, migra e troca a cor ativa do nginx (blue-green). Faz o deploy do HEAD
# como está (sem fetch nem checkout); recusa um checkout com alterações locais em arquivos versionados.
deploy up:
	RUINA_SKIP_FETCH=1 deploy/deploy.sh deploy "$$(git rev-parse HEAD)"

down:
	docker compose down

# Removes containers, locally-built images and orphans. Named data volumes
# (pgdata, images) are deliberately preserved - Técnico R0008 requires the
# destructive variant to be a separate, explicit target.
clean:
	docker compose down --rmi local --remove-orphans

# DESTRUCTIVE: also deletes the named data volumes (pgdata, images).
clean-data:
	docker compose down --rmi local --volumes --remove-orphans

logs:
	docker compose logs -f

migrate:
	deploy/deploy.sh migrate

# Cor ativa, releases e os backups mais recentes.
status:
	deploy/deploy.sh status

# Volta para a release anterior (um nível). Num deploy em janela, restaura o backup da janela.
rollback:
	deploy/deploy.sh rollback

grant-rules-auditor:
	deploy/deploy.sh exec-api --grant-rules-auditor $(EMAIL)

revoke-rules-auditor:
	deploy/deploy.sh exec-api --revoke-rules-auditor $(EMAIL)

# Prints a temporary password for the GM account with this email — that GM must log in with it
# and will be forced to set a new password before using the rest of the panel. No mailing service
# is configured (Técnico R0003), so it's up to the operator to hand the temporary password to the
# GM out-of-band.
reset-gm-password:
	deploy/deploy.sh exec-api --reset-gm-password $(EMAIL)
