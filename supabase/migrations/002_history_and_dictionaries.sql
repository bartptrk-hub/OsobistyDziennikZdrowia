-- Existing installation: run ONLY this migration, once, after 001_initial.sql.
-- No existing clinical records are removed or renamed.
begin;

alter table public.symptoms add column origin_id uuid;
alter table public.symptoms add constraint symptoms_id_patient_unique unique(id, patient_id);
alter table public.symptoms add constraint symptoms_origin_fk
  foreign key(origin_id, patient_id) references public.symptoms(id, patient_id) on delete restrict;
alter table public.symptoms add constraint symptoms_not_self check (origin_id is null or origin_id <> id);
create index symptoms_origin_idx on public.symptoms(origin_id, patient_id);

create function public.validate_symptom_origin() returns trigger
language plpgsql security invoker set search_path = '' as $$
declare source_start date;
begin
  if tg_op = 'UPDATE' and new.origin_id is distinct from old.origin_id then
    raise exception 'Symptom origin cannot be reassigned' using errcode = '23514';
  end if;
  if new.origin_id is not null then
    select s.start_date into source_start from public.symptoms s
      where s.id = new.origin_id and s.patient_id = new.patient_id for update;
    if not found or new.start_date < source_start then
      raise exception 'Invalid symptom origin or chronology' using errcode = '23514';
    end if;
  end if;
  if exists (select 1 from public.symptoms s where s.origin_id = new.id and s.start_date < new.start_date) then
    raise exception 'Source starts after its linked symptom' using errcode = '23514';
  end if;
  return new;
end;
$$;
create trigger validate_symptom_origin before insert or update on public.symptoms
for each row execute function public.validate_symptom_origin();
revoke all on function public.validate_symptom_origin() from public, anon, authenticated;

-- One atomic operation: insert a related symptom and optionally end its source.
-- SECURITY INVOKER keeps the existing patient RLS policies in force.
create function public.transition_symptom(p_symptom jsonb, p_close_source boolean)
returns public.symptoms language plpgsql security invoker set search_path = '' as $$
declare target public.symptoms; existing public.symptoms; source public.symptoms;
begin
  target := jsonb_populate_record(null::public.symptoms, p_symptom);
  if target.origin_id is null or target.id is null then
    raise exception 'Symptom id and origin are required' using errcode = '23514';
  end if;
  select * into source from public.symptoms s
    where s.id = target.origin_id and s.patient_id = target.patient_id for update;
  if not found then raise exception 'Source is not accessible' using errcode = '42501'; end if;
  select * into existing from public.symptoms s where s.id = target.id;
  if found then
    if existing is distinct from target then
      raise exception 'This id has already been used' using errcode = '23505';
    end if;
    return existing; -- Safe retry after an unknown network outcome.
  end if;
  insert into public.symptoms select (target).* returning * into target;
  if p_close_source then
    update public.symptoms set end_date = least(coalesce(end_date, target.start_date), target.start_date)
      where id = source.id;
  end if;
  return target;
end;
$$;
revoke all on function public.transition_symptom(jsonb, boolean) from public, anon, authenticated;
grant execute on function public.transition_symptom(jsonb, boolean) to authenticated;

-- Names are suggestions, not references: renaming or deleting one must never
-- rewrite a doctor's name or dosage unit in a historical medical record.
create table public.dictionary_entries (
  id uuid primary key default gen_random_uuid(),
  owner_id uuid not null default auth.uid() references auth.users(id) on delete cascade,
  kind text not null check (kind in ('medication','unit','doctor','clinic','symptom','area')),
  value text not null check (value = btrim(value) and length(value) between 1 and 200),
  check (kind <> 'unit' or length(value) <= 50)
);
create unique index dictionary_entries_name_unique on public.dictionary_entries(owner_id, kind, lower(value));

-- This marker prevents deleted defaults being recreated on every login.
create table public.dictionary_settings (
  owner_id uuid primary key default auth.uid() references auth.users(id) on delete cascade
);
alter table public.dictionary_entries enable row level security;
alter table public.dictionary_settings enable row level security;
revoke all on public.dictionary_entries, public.dictionary_settings from anon, authenticated;
grant select, insert, update, delete on public.dictionary_entries to authenticated;
grant select, insert on public.dictionary_settings to authenticated;
create policy own_dictionary_select on public.dictionary_entries for select to authenticated using (owner_id = (select auth.uid()));
create policy own_dictionary_insert on public.dictionary_entries for insert to authenticated with check (owner_id = (select auth.uid()));
create policy own_dictionary_update on public.dictionary_entries for update to authenticated using (owner_id = (select auth.uid())) with check (owner_id = (select auth.uid()));
create policy own_dictionary_delete on public.dictionary_entries for delete to authenticated using (owner_id = (select auth.uid()));
create policy own_dictionary_settings_select on public.dictionary_settings for select to authenticated using (owner_id = (select auth.uid()));
create policy own_dictionary_settings_insert on public.dictionary_settings for insert to authenticated with check (owner_id = (select auth.uid()));

alter table public.medications drop constraint medications_unit_check;
alter table public.medications add constraint medications_unit_length_check check (length(btrim(unit)) between 1 and 50);

-- Called once per account on its first login after migration. Copies current
-- suggestions from ALL its patients and supplies starter units/symptoms/areas.
create function public.initialize_dictionaries() returns void
language plpgsql security invoker set search_path = '' as $$
declare inserted integer;
begin
  insert into public.dictionary_settings(owner_id) values (auth.uid()) on conflict do nothing;
  get diagnostics inserted = row_count;
  if inserted = 0 then return; end if;
  insert into public.dictionary_entries(owner_id, kind, value)
    select auth.uid(), suggestions.kind, btrim(suggestions.value)
    from (
      select 'unit' as kind, unnest(array['tabletka','ml','mg','g','pompek','psiknięć','psiknięć w dziurkę']) as value
      union all select 'symptom', unnest(array['Katar','Kaszel','Gorączka','Ból gardła','Ból brzucha'])
      union all select 'area', unnest(array['Układ oddechowy','Układ pokarmowy','Skóra','Głowa/Szyja','Układ kostno-stawowy','Ogólne'])
      union all select 'medication', name from public.medications
      union all select 'unit', unit from public.medications
      union all select 'doctor', doctor from public.visits
      union all select 'clinic', clinic from public.visits
      union all select 'symptom', name from public.symptoms
      union all select 'area', area from public.symptoms
    ) suggestions where length(btrim(suggestions.value)) > 0
    on conflict do nothing;
end;
$$;
revoke all on function public.initialize_dictionaries() from public, anon, authenticated;
grant execute on function public.initialize_dictionaries() to authenticated;
notify pgrst, 'reload schema';
commit;
