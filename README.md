# Zdrowie domowe — Blazor + Supabase

Publikacja przez GitHub i darmową usługę Render: [instrukcja krok po kroku](docs/PUBLIKACJA_RENDER.md).

Pierwsza wersja nowej aplikacji na podstawie pliku `kod dzialajacy - backup.html`.
Oryginał pozostaje bez zmian. Mimo rozszerzenia HTML zawiera komponent React/JSX
z Firebase; nie stanowi eksportu danych medycznych.

## Co już jest

- Blazor Web App, C#, .NET 10, interaktywność po stronie serwera (Interactive Server).
- Logowanie e-mailem i hasłem przez Supabase Auth, odświeżanie tokenów i wylogowanie.
- Wiele profili pacjentów na jednym koncie; każdy profil ma osobną historię.
- Dodawanie, edycja i usuwanie wizyt, objawów i leków; walidacja formularzy.
- Daty początku i końca objawów/terapii, zalecenia, rozpoznania, powiązanie leku z wizytą.
- Kalendarz podawania według pór dnia lub konkretnych godzin; zaznaczanie i cofanie pojedynczej dawki.
- Historia jako kalendarz dzień po dniu lub lista: zakres dat, wyszukiwanie, filtr obszaru i podgląd wpisów.
- Powiązania objawów „przeszło w…” ze strzałką, także przy zmianie charakteru objawu o tej samej nazwie.
- Sześć edytowalnych słowników: leki, jednostki, lekarze, placówki, objawy i obszary.
- Gotowe zakresy dat, kolory grup leków, zapamiętywana kolejność i harmonogramy według pór dnia lub godzin.
- Responsywny interfejs po polsku, jasny i ciemny wygląd.
- Relacyjny schemat PostgreSQL, RLS i ograniczenia poprawności danych.

## Pierwsze uruchomienie

1. Zainstaluj .NET SDK 10 (w tym środowisku jest już zainstalowany).
2. Utwórz projekt Supabase. W jego SQL Editor wykonaj **raz**, w nowej bazie,
   kolejno `supabase/migrations/001_initial.sql` oraz
   `supabase/migrations/002_history_and_dictionaries.sql` oraz
   `supabase/migrations/003_medication_schedule_and_colors.sql` i
   `supabase/migrations/004_default_patient.sql`. Skrypty działają
   w transakcjach; nie uruchamiaj ich ponownie na istniejącym schemacie.
3. W Supabase Authentication utwórz użytkownika z e-mailem i hasłem, z potwierdzonym
   adresem e-mail. Ta wersja nie zawiera publicznej rejestracji ani resetu hasła.
4. Skopiuj `src/HistoriaMedyczna/appsettings.Local.example.json` do
   `src/HistoriaMedyczna/appsettings.Local.json`. Wstaw URL projektu i klucz
   **publishable** (lub starszy **anon**). Nie używaj `service_role`/secret — te
   klucze omijają RLS. Plik lokalny jest wykluczony przez `.gitignore`.
5. Uruchom z katalogu głównego repozytorium:

   ```powershell
   dotnet run --project src/HistoriaMedyczna
   ```

6. Otwórz adres wypisany w terminalu. Zaloguj się, dodaj profil pacjenta, wizytę
   i lek. Następnie odnotuj podanie w kalendarzu.

Bez konfiguracji aplikacja wyświetla instrukcję. Nie tworzy zastępczej lokalnej
bazy i nie wysyła danych do starego Firebase.

## Aktualizacja działającej wersji — kalendarz, powiązania i słowniki

Jeśli masz już działającą bazę z pierwszej wersji:

1. W Supabase SQL Editor wykonaj **tylko** nowy skrypt
   `supabase/migrations/002_history_and_dictionaries.sql`. Nie uruchamiaj ponownie
   `001_initial.sql`. Migracja nie usuwa ani nie zmienia treści zapisanych wpisów.
2. Zatrzymaj uruchomioną aplikację (`Ctrl+C` w jej terminalu), uruchom ponownie
   `dotnet run --project src/HistoriaMedyczna` i zaloguj się.
3. W „Historii medycznej” wybierz zakres dat. Domyślny widok to kalendarz:
   romby oznaczają wizyty, zielone odcinki objawy, szaroniebieskie odcinki leki.
   Kliknij znacznik lub odcinek, aby otworzyć szczegóły i przejść do edycji.
   Powiązane objawy znajdują się bezpośrednio pod źródłem, z wcięciem i strzałką.
   Kolejne epizody tej samej nazwy mają osobne wiersze. Tę kolejność zachowują
   również lista historii i zakładka objawów; filtry mogą ukryć źródło.
   Kilka wizyt tego samego dnia i nakładające się epizody mają osobne przyciski.
   Kalendarz przewija się w poziomie i pokazuje do 366 dni; lista nie ma tego limitu.
   Filtr obszaru dotyczy objawów, a wyszukiwanie wszystkich rodzajów zdarzeń.
4. Przy objawie kliknij „↳ Przeszło w…”, podaj nową nazwę lub zachowaj obecną
   i opisz zmianę charakteru. Wybierz początek nowego objawu. Opcja
   „Zakończ dotychczasowy” jest domyślnie odznaczona: poprzedni objaw zachowuje
   swoją datę zakończenia (lub jej brak). Zaznacz ją, jeśli ma skończyć się
   w dniu rozpoczęcia nowego.
   Wcześniejsza data zakończenia źródła nie jest wydłużana. Zapis obu zmian jest
   atomowy. Strzałka pojawia się w dniu początku nowego epizodu, a podgląd umożliwia
   przechodzenie między źródłem i jego następstwami. Jeśli źródło jest poza filtrem,
   powiązanie nadal widać w podglądzie. Status „Przeszło w: …” wynika z zapisanych
   relacji `origin_id`; nie jest niezależnym tekstem, który mógłby stracić aktualność.
5. W „Słownikach danych” dodawaj, edytuj i usuwaj nazwy w sześciu kategoriach.
   Przy pierwszym logowaniu słowniki otrzymują wartości z istniejących wpisów
   wszystkich Twoich pacjentów oraz podstawowe jednostki, objawy i obszary.
   Usunięte wartości nie wracają po ponownym logowaniu. Zmiany słowników dotyczą
   podpowiedzi, a nie historii: np. zmiana nazwy jednostki nie zmienia dawkowania
   zapisanego wcześniej. Wpisanie nowej nazwy bezpośrednio w formularzu medycznym
   nie dopisuje jej automatycznie do słownika — dodaj ją w zakładce słowników.

Powiązania można tworzyć tylko między objawami jednego pacjenta. Nowy objaw nie
może zaczynać się przed źródłowym. Źródła z powiązanymi objawami nie można usunąć;
najpierw usuń następstwa, jeśli powiązanie zostało zapisane omyłkowo. Słowniki są
prywatne dla konta właściciela; zakładka nie daje dostępu do danych innych kont.

Widok ma kompaktowe odstępy, mniejsze karty i węższe kolumny kalendarza.
Na telefonie nawigację otwiera przycisk „Menu”. Słowniki są na dole menu, tuż
nad wyborem jasnego/ciemnego wyglądu. Pola formularzy zachowują czytelny rozmiar, a historia
przewija się wewnątrz kalendarza. Te zmiany układu i kolejności nie wymagają
nowej migracji bazy; wystarczy ponownie uruchomić aplikację po aktualizacji kodu.

## Aktualizacja — zakresy dat, kolory, kolejność i godziny leków

Po wykonaniu migracji 001 i 002 uruchom **tylko nową migrację**
`supabase/migrations/003_medication_schedule_and_colors.sql` w Supabase SQL Editor.
Następnie zatrzymaj aplikację (`Ctrl+C`), uruchom ponownie i odśwież stronę.

- Lista „Okres” w historii oferuje ostatnie 7/14 dni, poprzedni/bieżący
  tydzień, poprzedni/obecny miesiąc i kolejne 7/14 dni. Tygodnie zaczynają się
  w poniedziałek. Ostatnie/kolejne dni uwzględniają dzisiaj, pełne miesiące i tygodnie
  mogą obejmować dni przyszłe. Ręczna zmiana dat przełącza listę na „Własne daty”.
- W edycji leku wybierz kolor grupy. Lista leków pokazuje go jako grube prawe
  obramowanie, harmonogram jako kolor kafelka, historia jako kolor odcinka.
  Harmonogram i historia nie mają dodatkowego kolorowego obramowania.
  Kolory są przypisane do konkretnych wpisów terapii; kolejne epizody tego samego
  leku mogą mieć różne kolory.
- Przeciągnij uchwyt ⋮⋮ na inny lek: przenoszony lek wstawiany jest przed docelowy.
  Strzałki ↑ ↓ służą do precyzyjnego przesuwania, także na telefonie i z klawiatury.
  Kolejność jest wspólna dla historii, harmonogramu i listy leków oraz zapisana
  w Supabase dla danego pacjenta. W harmonogramie obowiązuje wewnątrz każdej pory,
  ale nie ma tam przeciągania ani strzałek — zmiany wykonuje się w historii lub
  na liście leków. Strzałki w historii mają zwarty układ obok nazwy.
  Zapis kolejności jest atomowy i nie nadpisuje dawkowania. Jeśli inna osoba
  dodała lub usunęła lek w międzyczasie, odśwież listę przed ponowieniem.
- W formularzu wybierz „Pory dnia” lub „Konkretne godziny”. Dla godzin wybierz
  czas i kliknij „Dodaj godzinę”; możesz podać kilka różnych godzin w formacie
  HH:mm. Zmiana trybu czyści wybrane pory. Godziny są planem lokalnego czasu,
  bez powiadomień i alarmów. Dla nowego dawkowania zakończ poprzednią terapię
  i utwórz nowy wpis, aby zachować czytelność historii.
- „Rano” nazywa się teraz „rano (po posiłku)”. Migracja zmienia również etykietę
  już zapisanych podań, zachowując ich identyfikatory i daty. Zmiana nazwy nie
  stanowi nowego zalecenia dotyczącego przyjmowania leku.

Migracja 003 jest wymagana przed użyciem tej wersji aplikacji. Nie wykonuj
ponownie wcześniejszych migracji na istniejącej bazie.

## Lista leków dla przedszkola lub opiekunów

W „Kalendarzu podawania” lub „Lekach i suplementach” wybierz „Lista leków do
wydruku”. Podaj zakres dat i zaznacz pory/godziny, które mają być przekazane
opiekunowi. Kliknij „Wygeneruj listę”. Plan zawiera imię pacjenta, zakres dat
oraz osobną sekcję każdego dnia: jedna data nad tabelą, a pod nią pora/godzina,
nazwa leku, jednorazowa dawka z jednostką i pusta kratka „Podano” do zaznaczenia
długopisem. Taki sam układ ma podgląd, wydruk/PDF i pobrany HTML.
Uwzględnia tylko terapie aktywne danego dnia. Nie zawiera diagnoz, objawów ani
informacji o lekarzach. To plan podań, a nie potwierdzenie już przyjętych dawek.

„Drukuj / zapisz PDF” otwiera okno drukowania przeglądarki. Możesz wybrać drukarkę
lub zapis jako PDF. „Pobierz listę HTML” zapisuje samodzielny plik, który można
otworzyć, przekazać i wydrukować bez logowania do aplikacji. Wybór dat lub pór
czyści wcześniejszy podgląd — wygeneruj listę ponownie po zmianach. Zakres może
obejmować do 366 dni. Generator i poprawki widoku nie wymagają dodatkowej migracji.

Wydruk ma kompaktowe odstępy, stałe proporcje kolumn i zawijanie długich nazw.
Podgląd i pobrany HTML używają tego samego arkusza stylów, również przy drukowaniu
na A4. Po aktualizacji wygeneruj i pobierz listę ponownie — wcześniej pobrany plik
HTML zachowuje swój stary układ.

## Profil główny pacjenta

W działającej bazie po migracjach 001–003 wykonaj tylko nowy skrypt
`supabase/migrations/004_default_patient.sql`, a następnie uruchom ponownie aplikację.
Migracja wybiera początkowo pierwszy profil według nazwy na każdym koncie.
Wybierz żądanego pacjenta i kliknij „☆ Ustaw jako główny”. Gwiazdka w liście
oraz oznaczenie „★ Profil główny” potwierdzają zapis. Ustawienie jest przechowywane
w Supabase, prywatne dla konta i dostępne na wszystkich jego urządzeniach.

Po następnym logowaniu aplikacja otwiera główny profil. Podczas bieżącej sesji
możesz przełączyć się na inny; odświeżenie danych nie zmienia tego wyboru.
Pierwszy dodany profil na nowym koncie automatycznie staje się głównym.
Logowanie nadal jest wymagane po przeładowaniu aplikacji; wybór głównego profilu
nie zapisuje hasła ani sesji logowania w przeglądarce.

Otwarte objawy w historii kończą się wizualnie na dzisiejszej dacie. Nie zmienia
to ich zapisanej daty zakończenia. Wizyty zaplanowane na przyszłość pozostają
widoczne w wybranym zakresie.

W hostingu można zamiast pliku ustawić zmienne `Supabase__Url` i
`Supabase__PublishableKey`. Nie stosuj jednocześnie pliku lokalnego i zmiennych:
plik lokalny jest ładowany na końcu i ma pierwszeństwo. Hosting musi obsługiwać
ASP.NET Core i trwałe połączenia SignalR/WebSocket. Używaj HTTPS.

## Budowa i sprawdzenie

Przed ponowną kompilacją lub aktualizacją zatrzymaj działającą aplikację przez
`Ctrl+C` w terminalu, w którym ją uruchomiono (w debuggerze: zatrzymaj debugowanie).
Samo zamknięcie karty przeglądarki nie zatrzymuje serwera. Jeśli kompilacja zgłasza
`MSB3026`, `MSB3027` lub `MSB3021` i informację o zablokowanym pliku
`HistoriaMedyczna.exe`, poprzednia instancja nadal działa. Zatrzymaj ją i ponów
polecenie; nie trzeba zmieniać bazy ani ponownie wykonywać migracji SQL.

```powershell
dotnet build src/HistoriaMedyczna/HistoriaMedyczna.csproj
dotnet run --project checks/HistoriaMedyczna.Checks
```

Sprawdzenia bez zewnętrznych pakietów testowych obejmują walidację, daty terapii,
mapowanie JSON, tokeny, izolację sesji, stronicowanie, idempotencję oznaczania dawek,
renderowanie kalendarza i podglądu oraz wysyłanie powiązań przez pojedyncze RPC.
Korzystają z podstawionego serwera HTTP. Nie zastępują testów na prawdziwej bazie.
Skrypt `supabase/tests/access.sql` służy do sprawdzenia RLS i powiązań w testowym
projekcie przez SQL Editor; na końcu wycofuje dane testowe.
Po obu migracjach uruchom też `supabase/tests/002_features.sql`: sprawdza słowniki,
izolację między kontami, zachowanie historii, atomowe powiązania, powtórzenia zapisu
i chronologię objawów. Skrypt również wycofuje swoje dane testowe.
Po migracji 003 uruchom też `supabase/tests/003_medications.sql`: sprawdza kolory,
godziny, poprawność podań, zapis kolejności i odmowę zmiany cudzych leków.
Po migracji 004 uruchom `supabase/tests/004_default_patient.sql`: sprawdza zmianę
profilu głównego, unikalność tego wyboru i izolację kont.

Opcjonalny lokalny PostgreSQL (PGlite, bez połączenia z Supabase):

```powershell
npm install --prefix .artifacts/sql-checks @electric-sql/pglite --no-audit --no-fund
node checks/verify-sql.mjs
```

Ten test wykonuje wszystkie migracje i skrypty SQL z lokalnym odpowiednikiem
`auth.uid()`. Nie zastępuje weryfikacji prawdziwego Supabase Auth i REST API.

Przed korzystaniem z rzeczywistych danych sprawdź na dwóch kontach: brak wglądu
w cudze profile oraz odmowę dopisywania/zmiany/usuwania cudzych wpisów. Sprawdź
także zapis, odczyt po ponownym logowaniu, edycję, usuwanie oraz równoległe
oznaczenie tej samej dawki na dwóch urządzeniach.

## Struktura i decyzje

- `src/HistoriaMedyczna/Models`: modele i reguły walidacji.
- `src/HistoriaMedyczna/Services/SupabaseService.cs`: Supabase Auth i REST API,
  bez dodatkowego SDK. Każde żądanie danych używa JWT zalogowanego użytkownika.
- `src/HistoriaMedyczna/Components/Pages/Dashboard*`: formularze i widoki.
- `supabase/migrations/001_initial.sql`: pacjenci, wizyty, objawy, leki, podania dawek.
- `supabase/migrations/002_history_and_dictionaries.sql`: relacje objawów, atomowe RPC i słowniki konta.
- `supabase/migrations/003_medication_schedule_and_colors.sql`: godziny, kolory, zapisywanie kolejności i migracja porannych podań.
- `src/HistoriaMedyczna/Components/HistoryCalendar.razor`: przewijana siatka historii.
- `src/HistoriaMedyczna/Components/HistoryPreview.razor`: podgląd wpisów i ich powiązań.

Tokeny są przechowywane tylko w pamięci serwera w zakresie pojedynczej sesji
Blazora (circuit). Odświeżenie karty/restart serwera wymaga ponownego logowania;
chwilowe rozłączenie może zostać wznowione przez Blazor. Nie ma tokenów w localStorage.
Nie stosujemy publicznych kolekcji współdzielonych przez anonimowe konta.
RLS przypisuje profile do konta właściciela. Złożone klucze obce nie pozwalają
powiązać leku z wizytą innego pacjenta ani dawki z lekiem innego pacjenta.

Każdy wpis ma UUID. Dziennik podawania ma unikalność `(lek, dzień, pora)`:
równoczesne oznaczenie tej samej dawki nie tworzy duplikatów i nie nadpisuje innych
dawek. Pory to etykiety, nie dokładne godziny. Daty „dzisiaj” używają Europe/Warsaw.
Odświeżanie danych jest ręczne, przyciskiem „Odśwież” — nie ma jeszcze Realtime.
Edycja całego wpisu z dwóch urządzeń stosuje ostatni zapis.

Usunięcie leku z historią podań jest blokowane przez bazę; zakończ terapię datą.
Przed usunięciem wizyty odłącz jej leki. Zmiana dawkowania istniejącego wpisu
zmienia harmonogram całego jego okresu: dla nowego dawkowania zakończ stary wpis
i utwórz nowy. Historyczne podania pozostają w bazie, ale kalendarz pokazuje
harmonogram wynikający z aktualnej definicji terapii.

## Kolejne etapy względem starej aplikacji

- Współdzielenie pacjentów przez osobne konta rodziców: zaproszenia, członkostwo
  i role opiekunów oraz osobne testy RLS. Obecnie każdy właściciel widzi swoje profile.
- Realtime, zapamiętane logowanie, rejestracja/reset hasła i trwałe preferencje wyglądu.
- Import danych z Firebase po przygotowaniu rzeczywistego eksportu. Załączony kod
  zawiera konfigurację i logikę, a nie zapisane wizyty, objawy czy leki.
- Wersjonowanie dawkowania, eksport historii, załączniki i ewentualne przypomnienia.

Przy projektowaniu integracji wykorzystano oficjalne dokumentacje:
[Blazor render modes](https://learn.microsoft.com/aspnet/core/blazor/components/render-modes?view=aspnetcore-10.0),
[Supabase Auth](https://supabase.com/docs/guides/auth),
[Row Level Security](https://supabase.com/docs/guides/database/postgres/row-level-security).
