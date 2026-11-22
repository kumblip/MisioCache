# MisioCache — naprawa testu .NET po dodaniu Smart Downloads

## Przyczyna błędu

`tests/dotnet/Program.cs` traktował dowolne `AddTransient<IStartupFilter` jako pozostawioną telemetrię HTTP.
Tymczasem nowy `MisioCacheSmartDownloadsStartupFilter` jest celowo rejestrowany przez
MisioCache, aby załadować skrypt klienta do HTML Jellyfina. Nie jest obserwatorem HTTP.

## Zmiana

Test nadal odrzuca `MisioCachePassiveHttpStartupFilter`, `MisioCacheHttpObservationStore`,
`MisioCacheResponseByteMeter`, stare flagi i elementy diagnostyki HTTP, a dodatkowo
wymaga obecności `MisioCacheSmartDownloadsStartupFilter` oraz aktualizacji Jellyfin.
Nie zmieniono żadnego pliku kodu produkcyjnego, `.github`, metadanych ani manifestu.

## Instalacja

Zawartość archiwum rozpakuj do głównego katalogu repozytorium `kumblip/MisioCache`,
z zachowaniem ścieżek, nadpisując `tests/dotnet/Program.cs`.
Commit do `main` uruchomi istniejące GitHub Actions.

## Weryfikacja lokalna

33 testy Python: 32 OK, 1 pominięty z powodu braku .NET SDK;
sprawdzono warunki testu .NET na rzeczywistych plikach źródłowych.
Nie uruchamiano .NET lokalnie — kompilację i wykonanie potwierdzi GitHub Actions.
