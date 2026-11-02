-- Run once in a NEW Supabase project using SQL Editor, or via supabase db push.
begin;

create table public.patients (
  id uuid primary key default gen_random_uuid(),
  owner_id uuid not null default auth.uid() references auth.users(id) on delete cascade,
  name text not null check (length(btrim(name)) between 1 and 120)
);
create index patients_owner_idx on public.patients(owner_id);

create table public.visits (
  id uuid primary key default gen_random_uuid(),
  patient_id uuid not null references public.patients(id) on delete cascade,
  date date not null,
  doctor text not null check (length(btrim(doctor)) between 1 and 200),
  clinic text not null default '' check (length(clinic) <= 200),
  diagnosis text not null default '' check (length(diagnosis) <= 500),
  recommendations text not null default '' check (length(recommendations) <= 10000),
  unique(id, patient_id)
);
create index visits_patient_date_idx on public.visits(patient_id, date);

create table public.symptoms (
  id uuid primary key default gen_random_uuid(),
  patient_id uuid not null references public.patients(id) on delete cascade,
  name text not null check (length(btrim(name)) between 1 and 200),
  area text not null default '' check (length(area) <= 200),
  description text not null default '' check (length(description) <= 10000),
  start_date date not null,
  end_date date check (end_date >= start_date)
);
create index symptoms_patient_date_idx on public.symptoms(patient_id, start_date);

create table public.medications (
  id uuid primary key default gen_random_uuid(),
  patient_id uuid not null references public.patients(id) on delete cascade,
  name text not null check (length(btrim(name)) between 1 and 200),
  dose numeric not null check (dose > 0 and dose <= 1000000),
  unit text not null check (unit in ('tabletka','ml','mg','g','pompek','psiknięć','psiknięć w dziurkę')),
  start_date date not null,
  end_date date check (end_date >= start_date),
  times_of_day text[] not null check (
    cardinality(times_of_day) between 1 and 7
    and array_position(times_of_day, null) is null
    and times_of_day <@ array['rano (na czczo)','rano','przed obiadem','w trakcie obiadu','po obiedzie','do kolacji','przed snem']::text[]
  ),
  visit_id uuid,
  foreign key (visit_id, patient_id) references public.visits(id, patient_id) on delete restrict,
  unique(id, patient_id)
);
create index medications_patient_date_idx on public.medications(patient_id, start_date);
create index medications_visit_idx on public.medications(visit_id, patient_id);

create table public.dose_logs (
  id uuid primary key default gen_random_uuid(),
  patient_id uuid not null references public.patients(id) on delete cascade,
  medication_id uuid not null,
  date date not null check (date <= (now() at time zone 'Europe/Warsaw')::date),
  time_of_day text not null check (time_of_day in ('rano (na czczo)','rano','przed obiadem','w trakcie obiadu','po obiedzie','do kolacji','przed snem')),
  given_at timestamptz not null default now(),
  foreign key (medication_id, patient_id) references public.medications(id, patient_id) on delete restrict,
  unique(medication_id, date, time_of_day)
);
create index dose_logs_patient_date_idx on public.dose_logs(patient_id, date);

-- Validate dose date and schedule in the database, including direct REST calls.
create function public.validate_dose() returns trigger language plpgsql
set search_path = '' as $$
begin
  if not exists (
    select 1 from public.medications m where m.id = new.medication_id
      and m.patient_id = new.patient_id and new.date >= m.start_date
      and (m.end_date is null or new.date <= m.end_date)
      and new.time_of_day = any(m.times_of_day)
  ) then
    raise exception 'Dose is outside the medication schedule' using errcode = '23514';
  end if;
  return new;
end;
$$;
create trigger validate_dose before insert or update on public.dose_logs
for each row execute function public.validate_dose();

alter table public.patients enable row level security;
alter table public.visits enable row level security;
alter table public.symptoms enable row level security;
alter table public.medications enable row level security;
alter table public.dose_logs enable row level security;

revoke all on public.patients, public.visits, public.symptoms, public.medications, public.dose_logs from anon, authenticated;
grant select, insert, update, delete on public.patients, public.visits, public.symptoms, public.medications, public.dose_logs to authenticated;
revoke all on function public.validate_dose() from public;

create policy patients_select on public.patients for select to authenticated using (owner_id = (select auth.uid()));
create policy patients_insert on public.patients for insert to authenticated with check (owner_id = (select auth.uid()));
create policy patients_update on public.patients for update to authenticated using (owner_id = (select auth.uid())) with check (owner_id = (select auth.uid()));
create policy patients_delete on public.patients for delete to authenticated using (owner_id = (select auth.uid()));

-- The patient policy scopes all child records. Composite FKs prevent links
-- across patients, even when both patients belong to the same account.
do $$
declare table_name text;
begin
  foreach table_name in array array['visits','symptoms','medications','dose_logs'] loop
    execute format('create policy own_patient_select on public.%I for select to authenticated using (exists (select 1 from public.patients p where p.id = patient_id))', table_name);
    execute format('create policy own_patient_insert on public.%I for insert to authenticated with check (exists (select 1 from public.patients p where p.id = patient_id))', table_name);
    execute format('create policy own_patient_update on public.%I for update to authenticated using (exists (select 1 from public.patients p where p.id = patient_id)) with check (exists (select 1 from public.patients p where p.id = patient_id))', table_name);
    execute format('create policy own_patient_delete on public.%I for delete to authenticated using (exists (select 1 from public.patients p where p.id = patient_id))', table_name);
  end loop;
end;
$$;
commit;
