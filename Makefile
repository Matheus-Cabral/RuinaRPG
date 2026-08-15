.PHONY: deploy up down clean logs migrate

deploy up:
	docker compose --env-file .env up --build -d

down:
	docker compose down

clean:
	docker compose down --rmi local --volumes --remove-orphans

logs:
	docker compose logs -f

migrate:
	docker compose exec api dotnet RuinaRPG.Api.dll --migrate
