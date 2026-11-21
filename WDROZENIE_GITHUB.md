# MisioCache — publikacja w GitHub krok po kroku

Repozytorium: https://github.com/kumblip/MisioCache

## Krok 1. Umieść pliki na main

Rozpakuj paczkę `MisioCache_v0.1.0.0_GitHub_PELNE.zip` i wgraj **zawartość katalogu**, nie katalog jako podfolder. Muszą znaleźć się w głównym katalogu: `meta.json`, `manifest.json`, `src/`, `tools/`, `tests/`, `.github/workflows/`.

Metoda pewna — Git + terminal w katalogu rozpakowanych plików:

```sh
git init
git branch -M main
git add -A
git commit -m "feat: start MisioCache 0.1"
git remote add origin https://github.com/kumblip/MisioCache.git
git push -u origin main
```

Jeśli konto poprosi o logowanie, skorzystaj z GitHub Desktop albo uwierzytelnienia w Git Credential Manager; nie podawaj haseł ani tokenów w czacie.

Możesz też wgrać pliki przez przeglądarkę. Po zatwierdzeniu sprawdź w repo sekcję `.github/workflows` (ukryte foldery mogą wymagać osobnego wgrania).

## Krok 2. Uprawnienia workflow

GitHub → repo `MisioCache` → **Settings** → **Actions** → **General** → **Workflow permissions** → **Read and write permissions** → **Save**. Jeżeli wymuszone są reguły organizacji/branch protection, dostosuj je do automatycznych commitów manifestu.

## Krok 3. Automatyczny Build i Release

Otwórz kartę **Actions** → workflow **MisioCache — automatyczny Build i Release**. Po pierwszym push na `main` workflow wykonuje sam:

1. Testy Pythona i walidację spójności wersji.
2. Kompilację DLL .NET 10 dla Jellyfin 12.4.
3. Pakowanie `MisioCache_0.1.0.0_JF12.4.zip` z `Jellyfin.Plugin.MisioCache.dll` oraz `meta.json`.
4. Publikację tagu `v0.1.0.0`, paczki i sum kontrolnych w **Releases**.
5. Dopisanie URL oraz MD5 do manifestu repozytorium Jellyfin i commit w `main`.

Po kolejnych zmianach w `src/`, `tools/`, `tests/`, pliku `meta.json` lub workflow powstaje wersja `0.1.1.0`, następnie `0.1.2.0`. Zmiana tylko README/manifest nie odpala wydania.

W razie czerwonego X w Actions: wejdź w nieudaną próbę i skopiuj tekst błędu z kroku, który zawiódł. **Nie przechodź do instalacji nieudanego wydania.** Workflow kontrolny **MisioCache — testy i Build kontrolny** można uruchomić ręcznie w **Actions** bez wydawania nowej wersji.

## Krok 4. Instalacja z repozytorium wtyczek Jellyfin

W Jellyfin wejdź w **Panel administratora → Wtyczki → Repozytoria**, dodaj:

`https://raw.githubusercontent.com/kumblip/MisioCache/main/manifest.json`

Odśwież katalog, znajdź `MisioCache`, zainstaluj i uruchom ponownie Jellyfin.

Następnie zobaczysz w menu administratora `MisioCache` oraz `MisioCache — ustawienia`. W drugim wpisie odczytaj diagnostykę przeglądarki i zapisz docelową politykę cache.

**Wersja 0.1 NIE pobiera filmu do cache.** Nie wymaga żadnych zmian w MisioFlix ani w obecnym odtwarzaczu.

## Krok 5. Kolejne aktualizacje

Zmodyfikowany kod zatwierdź na `main` przyciskiem **Commit changes** lub wykonaj `git push`. Numer release, paczka instalacyjna i manifest powstają automatycznie. Jellyfin znajdzie nową wersję przy sprawdzaniu katalogów — czas i automatyka instalacji zależą od konfiguracji samego Jellyfina; po aktualizacji wtyczki restart może być konieczny.

## Awaryjny rollback

Jeśli następna wersja ma problem, pobierz wcześniejszy ZIP z Releases lub wyłącz/usuń nowszą wtyczkę według panelu Jellyfina. MisioFlix v0.4.121.0 nie jest dotykany.
