# Deploying ClinicLive to a Linux VPS

> Part 12 of the series. Placeholders throughout: `203.0.113.10` is a
> documentation-reserved IP and `cliniclive.example.com` a placeholder domain —
> substitute your own. Nothing here is a real server.

## One-time server setup

```bash
# On the VPS (Ubuntu), as a sudo user:
sudo apt update
sudo apt install -y nginx postgresql-18 dotnet-runtime-10.0 aspnetcore-runtime-10.0

# Database + login role for the app
sudo -u postgres psql -c "CREATE USER cliniclive WITH PASSWORD 'change-me';"
sudo -u postgres psql -c "CREATE DATABASE cliniclive OWNER cliniclive;"

sudo mkdir -p /var/www/cliniclive
```

Production settings live ONLY on the server — never in git:

```bash
# /var/www/cliniclive/appsettings.Production.json  (chmod 600)
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=cliniclive;Username=cliniclive;Password=change-me"
  },
  "Clinic": { "TimeZone": "Australia/Sydney" }
}
```

Install the service and nginx site from this folder:

```bash
sudo cp cliniclive.service /etc/nginx/../systemd/system/   # i.e. /etc/systemd/system/
sudo cp nginx-cliniclive.conf /etc/nginx/sites-available/cliniclive.example.com
sudo ln -s /etc/nginx/sites-available/cliniclive.example.com /etc/nginx/sites-enabled/
sudo systemctl enable cliniclive
sudo nginx -t && sudo systemctl reload nginx
```

## Ship an update (from the dev machine)

```bash
dotnet publish src/ClinicLive/ClinicLive.csproj -c Release -o bin/deploy-linux
tar -czf cliniclive.tar.gz -C bin/deploy-linux .          # tar, not zip: Windows
                                                          # zips break paths on Linux
scp cliniclive.tar.gz youruser@203.0.113.10:~
ssh youruser@203.0.113.10 "sudo systemctl stop cliniclive \
  && sudo tar -xzf ~/cliniclive.tar.gz -C /var/www/cliniclive \
  && sudo chown -R www-data:www-data /var/www/cliniclive \
  && sudo systemctl start cliniclive"
```

Migrations apply automatically at startup (`DbSeeder.SeedAsync` → `MigrateAsync`).
For teams and zero-downtime setups, run `dotnet ef database update` as a deploy
step instead — startup migration is the honest small-app shortcut, and the series
discusses the trade-off.

## The checklist that saves you at 2am

- [ ] `sudo journalctl -u cliniclive -f` — the app's logs, live
- [ ] Board stuck on "reconnecting…"? You forgot the SIGNALR headers in nginx
- [ ] `502 Bad Gateway`? The service isn't listening on 5100 — check journalctl
- [ ] Login loops? `ASPNETCORE_ENVIRONMENT=Production` + missing HTTPS forwarding
      headers — the `X-Forwarded-Proto` line in nginx fixes it
- [ ] Times look shifted? Set `Clinic:TimeZone` on the server (Part 10 says hello)
