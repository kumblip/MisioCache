# Historia MisioCache

## v0.1.0.0 — pierwsze wydanie bazowe

- Niezależny plugin Jellyfin 12.4/.NET 10, GUID niekolidujący z MisioFlix.
- Dwa wpisy do menu administratora: `MisioCache` i `MisioCache — ustawienia`.
- Panel diagnostyczny Storage API/HTTPS, zapis preferowanej przyszłej polityki cache w ustawieniach Jellyfina.
- Bezpieczna separacja od istniejącego odtwarzacza; brak aktywnego cache w tej wersji.
- Workflow Build kontrolny + automatyczny GitHub Release przy commit do main, czteroczłonowa wersja + ZIP/MD5/SHA256 + manifest repozytorium Jellyfin.
