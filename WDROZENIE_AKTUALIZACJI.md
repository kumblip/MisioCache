# MisioCache — przyciski wymuszania aktualizacji w Jellyfin

## Co zmienia aktualizacja

Po pierwszym udanym Build/Release nowej wersji zostanie dodany panel **Aktualizacje MisioCache**: wersja zainstalowana, wersja GitHub, wersja manifestu, wersja w katalogu Jellyfina, ostatnie sprawdzenie. **Wymuś aktualizację katalogu Jellyfina** zapisuje do konfiguracji natywnego repozytorium wersję manifestu przypiętą do rewizji SHA. Nie instaluje DLL. **Zainstaluj aktualizację** uruchamia standardowe polecenie Jellyfina na kliknięcie administratora, gdy jest zgodna wersja nowsza od zainstalowanej. Restart serwera może być wymagany.

## Wdrożenie w repozytorium GitHub

1. Wgraj zmiany do `https://github.com/kumblip/MisioCache` na `main` jako **Commit changes**. **Nie musisz ręcznie podnosić numeru**: standardowe GitHub Actions wybiera następny numer na podstawie tagów i manifestu. Gdy istnieje już Release `v0.1.0.0`, następna publikacja powinna nosić numer `v0.1.1.0`.
2. Otwórz **Actions → MisioCache — automatyczny Build i Release**. Sprawdź, czy testy, `dotnet publish`, pakowanie i `gh release create` się powiodły; manifest powinien zostać zaktualizowany.
3. Upewnij się, że w Jellyfin jest **jedno** aktywne repozytorium MisioCache: `https://raw.githubusercontent.com/kumblip/MisioCache/main/manifest.json`. Jeśli było ręcznie dodane pod tą nazwą, nie dodawaj drugiego.
4. W Jellyfin otwórz **MisioCache — ustawienia → Aktualizacje** i użyj **Wymuś aktualizację katalogu Jellyfina**. Musisz korzystać z konta administratora.
5. Gdy pojawi się wersja wyższa od obecnie zainstalowanej i zgodna z ABI 12.4.0.0, użyj **Zainstaluj aktualizację**. Po instalacji zrestartuj Jellyfin, jeżeli o to poprosi.

**Ważna kolejność pierwszej aktualizacji:** stara v0.1.0.0 nie ma jeszcze przycisku, więc zainstaluj nową wersję **raz** w standardowym katalogu wtyczek Jellyfina (Wtyczki → Katalog) lub przez dotychczasową metodę. Dopiero zaktualizowana wersja udostępni przyciski do kolejnych aktualizacji. Nie modyfikuj DLL ręcznie na działającym serwerze.

Automatyczne sprawdzanie co 15 min dotyczy wykrywania wersji. **Instalacja nie następuje automatycznie** i nie wpływa na odtwarzacz ani MisioFlix.
