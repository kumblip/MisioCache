# MisioCache v0.1.4.0 — workflow pozostają bez zmian

## Stały mechanizm GitHub

1. Commit użytkownika na gałęzi `main` uruchamia `.github/workflows/release.yml`.
2. Workflow wyznacza kolejny numer na podstawie istniejących tagów,
   synchronizuje `meta.json` i wersję zestawu DLL, wykonuje `tools/run_tests.py`,
   kompiluje źródła, publikuje GitHub Release i aktualizuje manifest.
3. `.github/workflows/build.yml` uruchamia kontrolne testy i Build dla PR
   albo na żądanie.
4. Wersja v0.1.4.0 nie zmienia żadnego z tych plików. Nie podmieniaj ich
   z innych paczek. Jeżeli instalowałeś wcześniejszą paczkę v0.1.4.0,
   pamiętaj, że mogła ona już dodać kroki do workflow; skorygowana pełna
   paczka zawiera niezmienione pliki z v0.1.3.0.

## Jak dodawać nowe testy bez zmiany GitHub Actions

- Testy Python: dodaj plik `tests/test_*.py`.
- Testy C#: dodaj projekt `tests/dotnet/**/Nazwa.csproj`.
- W obu workflow znajduje się `python3 tools/run_tests.py`.
- Skrypt uruchomi najpierw testy Python, a następnie wszystkie znalezione
  projekty `.csproj` przez `dotnet run --configuration Release`.
- Gdy test się nie powiedzie, Build/Release przerwie pracę.

## Wgrywanie poprawki

Wgraj ZIP **MisioCache_v0.1.4.0_TYLKO_ZMIANY_BEZ_WORKFLOW.zip**
przez Commit changes. Nie wgrywaj `manifest.json`. Automatyczna numeracja
może opublikować inny następny numer, jeśli istnieją już nowsze tagi.
