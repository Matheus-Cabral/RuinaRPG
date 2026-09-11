.PHONY: deploy up down clean clean-data logs migrate grant-rules-auditor revoke-rules-auditor

deploy up:
	docker compose --env-file .env up --build -d

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
	docker compose exec api dotnet RuinaRPG.Api.dll --migrate

grant-rules-auditor:
	docker compose exec api dotnet RuinaRPG.Api.dll --grant-rules-auditor $(EMAIL)

revoke-rules-auditor:
	docker compose exec api dotnet RuinaRPG.Api.dll --revoke-rules-auditor $(EMAIL)
