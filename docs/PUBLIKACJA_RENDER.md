# Publikacja: GitHub + Render Free + Supabase

Instrukcja dla obecnej aplikacji Blazor Interactive Server (.NET 10).
GitHub przechowuje kod, Render uruchamia serwer, Supabase przechowuje dane i konta.
Sourcetree służy do wysyłania zmian do GitHuba. GitHub Pages nie uruchomi tej
wersji aplikacji, ponieważ wymaga ona działającego serwera ASP.NET Core.

## 1. Przygotuj repozytorium na GitHubie

1. Zaloguj się do GitHuba i wybierz **New repository**.
2. Nazwij repozytorium np. `zdrowie-domowe` i wybierz **Private**.
3. Jeśli lokalny projekt ma już pliki, utwórz puste repozytorium: bez generowania
   README, `.gitignore` i licencji na GitHubie.
4. W Sourcetree wybierz **Create** (lub **Add**, jeśli folder jest już repozytorium).
   Wskaż `C:\Users\bpiotrkowski\Desktop\REPO\LEKI`.
5. W **File Status** sprawdź pliki, dodaj je do staged i wykonaj commit,
   np. `Przygotowanie publikacji aplikacji`.
6. W ustawieniach repozytorium dodaj remote `origin` wskazujący na adres nowego
   repozytorium GitHub. Wykonaj **Push** bieżącej gałęzi, np. `main`.
7. Na GitHubie sprawdź, czy widzisz `Dockerfile`, `render.yaml` oraz `src`.

`appsettings.Local.json` nie powinien pojawić się w staged ani na GitHubie.
`.gitignore` dotyczy plików nieśledzonych; jeśli plik został wcześniej dodany
do Gita, samo dopisanie reguły go nie usuwa. W takim przypadku zatrzymaj publikację
i usuń go ze śledzenia przed pushem. Nie wysyłaj haseł, eksportów danych ani kluczy
`service_role`/secret. Klucz publishable/anon jest przeznaczony do API użytkowników,
ale tę konfigurację i tak podajemy w panelu hostingu, poza kodem.

## 2. Sprawdź Supabase

Użyj obecnego projektu Supabase. Nie musisz tworzyć nowej bazy dla hostingu.
Migracje 001–004 mają być już wykonane. Nie uruchamiaj ich ponownie przy deployu.
Skopiuj z panelu projektu:

- URL, np. `https://twoj-projekt.supabase.co`;
- klucz **publishable** lub starszy **anon**.

Nie używaj hasła do PostgreSQL ani `service_role`/secret.

## 3. Utwórz aplikację w Render

1. Otwórz [Render](https://dashboard.render.com/) i załóż konto / zaloguj się.
2. Wybierz darmowy workspace **Hobby**. Nie dodawaj płatnych usług.
3. Wybierz **New → Blueprint** i połącz GitHuba. Nadaj integracji dostęp
   do wybranego repozytorium.
4. Wybierz repozytorium `zdrowie-domowe` i gałąź, na którą wysłałeś kod.
5. Render odczyta `render.yaml` i zaproponuje usługę `zdrowie-domowe`:
   typ Web Service, Docker, plan **Free**, region Frankfurt.
6. Uzupełnij dwie wartości, o które zapyta formularz:

   | Zmienna | Wartość |
   | --- | --- |
   | `Supabase__Url` | URL Twojego projektu Supabase |
   | `Supabase__PublishableKey` | Klucz publishable/anon |

   Nazwy zawierają **dwa podkreślenia**. Nie wklejaj ich do kodu ani `render.yaml`.
7. Przed zatwierdzeniem sprawdź, czy instancja ma plan **Free / $0**.
8. Zatwierdź utworzenie Blueprint i zaczekaj na build/deploy.
9. Po statusie **Live** otwórz adres `https://...onrender.com` z panelu usługi.

Render buduje kontener na swoich serwerach. Nie musisz instalować Dockera
na swoim komputerze. Nazwy pól w panelu mogą się zmieniać; konfigurację usługi
definiuje dołączony `render.yaml`.

Alternatywnie: **New → Web Service**, wskaż to samo repozytorium i wybierz
**Docker**, **Free**, region Frankfurt. Root Directory pozostaw puste,
Dockerfile Path: `./Dockerfile`, Health Check Path: `/healthz`.
W Environment dodaj dwie zmienne Supabase oraz `PORT=8080`,
`ASPNETCORE_HTTP_PORTS=8080`, `ASPNETCORE_ENVIRONMENT=Production`,
`Hosting__HttpsAtEdge=true`. Pozostaw komendę startową z Dockerfile.

## 4. Sprawdź wersję publiczną

1. Otwórz `https://...onrender.com/healthz` — powinno pokazać `{"status":"ok"}`.
   To sprawdza działanie serwera, a nie połączenie z bazą.
2. Otwórz stronę główną, zaloguj się i sprawdź wybór głównego pacjenta.
3. Na fikcyjnym profilu sprawdź zapis i odczyt po ponownym zalogowaniu,
   oznaczenie dawki, historię oraz wydruk listy leków.
4. Sprawdź na telefonie i drugim koncie, czy każda osoba widzi własne profile.

Jeżeli widzisz instrukcję konfiguracji zamiast logowania, sprawdź dwie zmienne
Supabase w **Environment** usługi i wykonaj ponowny deploy.
Jeżeli połączenie interaktywne się zerwie, odśwież stronę i zaloguj się ponownie.
Nie twórz dodatkowej bazy PostgreSQL w Render — aplikacja używa Supabase.

Jeżeli `/healthz` działa, ale strona jest całkowicie pusta, sprawdź adres
`https://TWOJA-APLIKACJA.onrender.com/_framework/blazor.web.js`. Musi zwrócić
JavaScript, a nie błąd 404. W .NET 10 przywracanie zależności przed skopiowaniem
plików `.razor` może pominąć zasoby startowe Blazora. Poprawiony `Dockerfile`
kopiuje źródła przed `dotnet restore`, a projekt ustawia
`RequiresAspNetWebAssets=true`. Budowa kontenera sprawdza obecność skryptu.
Po wysłaniu poprawki wybierz w Render **Manual Deploy → Clear build cache & deploy**.
Sama odpowiedź 200 ze strony głównej nie potwierdza uruchomienia interfejsu.

## 5. Udostępnij innym

Przekazujesz adres `https://...onrender.com`, nie pliki projektu.
Komputer, na którym tworzysz aplikację, nie musi pozostawać włączony.

Obecna wersja aplikacji nie ma samodzielnej rejestracji użytkowników:

1. W Supabase otwórz **Authentication → Users**.
2. Utwórz osobne konto z potwierdzonym e-mailem dla każdej zaproszonej osoby.
3. Przekaż jej adres aplikacji i dane do jej konta bezpiecznym kanałem.
4. Po zalogowaniu osoba dodaje własnego pacjenta i własne wpisy.

Nie udostępniaj wszystkim jednego loginu. Profile, słowniki i wybór głównego
pacjenta są przypisane do właściciela konta. Osobne konta rodziców nie mają
jeszcze funkcji współdzielenia tego samego pacjenta. Rejestracja i reset hasła
mogą być kolejnym etapem przed udostępnieniem aplikacji szerszej publiczności.

## 6. Kolejne aktualizacje

1. Zmieniasz kod i sprawdzasz aplikację lokalnie.
2. Jeżeli zmiana ma migrację SQL, wykonujesz ją raz w Supabase zgodnie z instrukcją.
3. W Sourcetree wykonujesz **Commit → Push** na podłączoną gałąź.
4. Render uruchamia deploy; status sprawdzasz w panelu.
5. Po wdrożeniu odświeżasz stronę. Restart/deploy może zakończyć sesje Blazora.

## Limity i koszty — stan sprawdzony 2 listopada 2026

Render Free nie ma tu prostego licznika „liczba wejść”, ale nie jest bezlimitowy:

- 512 MB RAM dla darmowej usługi;
- usypianie po 15 minutach bez ruchu; pierwsze wejście po uśpieniu może trwać
  około minuty;
- 750 godzin darmowych instancji miesięcznie na workspace, współdzielonych
  między darmowymi usługami; jedna usługa mieści się w tej puli w zwykłym miesiącu;
- workspace Hobby obejmuje 5 GB transferu miesięcznie oraz limity budowania;
- bez metody płatności przekroczenie transferu zawiesza darmowe usługi do końca
  miesiąca, zamiast naliczać dopłatę. Z metodą płatności nadwyżki mogą kosztować.

Render opisuje Free jako plan do projektów hobbystycznych/testów, bez gwarancji
ciągłej dostępności. Blazor Server utrzymuje stan aktywnych użytkowników w RAM:
duża liczba jednoczesnych użytkowników może wymagać większej instancji.

Supabase Free także ma limity: 500 MB bazy, 5 GB transferu wychodzącego,
50 000 aktywnych użytkowników miesięcznie i możliwość pauzy po tygodniu bezczynności.
Nie obejmuje automatycznych kopii zapasowych — zaplanuj własny eksport danych.

Źródła:

- [Render Free](https://render.com/docs/free)
- [Cennik Render](https://render.com/pricing)
- [Docker w Render](https://render.com/docs/docker)
- [Blueprints](https://render.com/docs/blueprint-spec)
- [WebSockets](https://render.com/docs/websocket)
- [HTTPS w Render](https://render.com/docs/tls)
- [Cennik Supabase](https://supabase.com/pricing)

## Ustawienia techniczne

`Hosting__HttpsAtEdge=true` jest przeznaczone dla Render, który wymusza publiczny
HTTPS i kończy TLS przed przekazaniem żądania do kontenera HTTP. Nie ustawiaj tej
opcji na serwerze wystawionym bezpośrednio do internetu przez HTTP. Aplikacja nie
włącza bezwarunkowego zaufania do przekazywanych nagłówków dowolnego proxy.

`appsettings.Local.json` jest wykluczony z kontekstu Dockera i pakietu `publish`.
Dane medyczne pozostają w Supabase. Dysk kontenera jest tymczasowy; aplikacja nie
przechowuje na nim historii medycznej. Sekrety sesji i tokeny są w pamięci:
po restarcie serwera użytkownik loguje się ponownie.
