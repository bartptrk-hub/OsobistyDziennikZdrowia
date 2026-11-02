-- Run once after 002. Preserves existing doses while renaming the morning slot.
begin;
alter table public.medications add column color text not null default 'sage'
  check (color in ('sage','blue','purple','amber','rose','slate'));
alter table public.medications add column display_order integer not null default 0;
alter table public.medications add column schedule_mode text not null default 'period'
  check (schedule_mode in ('period','clock'));
alter table public.medications drop constraint medications_times_of_day_check;
alter table public.dose_logs drop constraint dose_logs_time_of_day_check;

update public.medications set times_of_day = array_replace(times_of_day, 'rano', 'rano (po posiłku)');
update public.dose_logs set time_of_day = 'rano (po posiłku)' where time_of_day = 'rano';
with ranks as (
  select id, row_number() over(partition by patient_id order by name, id) - 1 as position from public.medications
) update public.medications m set display_order = ranks.position from ranks where m.id = ranks.id;

create function public.valid_medication_schedule(slots text[], mode text) returns boolean
language sql immutable security invoker set search_path = '' as $$
  select coalesce(cardinality(slots) between 1 and 24
    and array_position(slots, null) is null
    and (select count(*) = count(distinct s) from unnest(slots) s)
    and case when mode = 'period' then slots <@ array['rano (na czczo)','rano (po posiłku)','przed obiadem','w trakcie obiadu','po obiedzie','do kolacji','przed snem']::text[]
      when mode = 'clock' then (select bool_and(s ~ '^([01][0-9]|2[0-3]):[0-5][0-9]$') from unnest(slots) s)
      else false end, false);
$$;
revoke all on function public.valid_medication_schedule(text[],text) from public, anon;
grant execute on function public.valid_medication_schedule(text[],text) to authenticated;
alter table public.medications add constraint medications_schedule_check
  check (public.valid_medication_schedule(times_of_day, schedule_mode));
alter table public.dose_logs add constraint dose_logs_time_of_day_check check (
  time_of_day in ('rano (na czczo)','rano (po posiłku)','przed obiadem','w trakcie obiadu','po obiedzie','do kolacji','przed snem')
  or time_of_day ~ '^([01][0-9]|2[0-3]):[0-5][0-9]$'
);

create function public.reorder_medications(p_patient_id uuid, p_order uuid[]) returns void
language plpgsql security invoker set search_path = '' as $$
begin
  perform 1 from public.patients where id = p_patient_id for update;
  if not found then raise exception 'Patient not accessible' using errcode = '42501'; end if;
  if p_order is null or array_position(p_order,null) is not null
    or cardinality(p_order) <> (select count(*) from public.medications where patient_id = p_patient_id)
    or cardinality(p_order) <> (select count(distinct requested.id) from unnest(p_order) requested(id))
    or exists(select 1 from unnest(p_order) requested(id) where not exists(select 1 from public.medications m where m.id = requested.id and m.patient_id = p_patient_id)) then
    raise exception 'Medication list changed. Reload before reordering.' using errcode = '23514';
  end if;
  update public.medications m set display_order = o.position - 1
    from unnest(p_order) with ordinality o(id, position) where m.id = o.id and m.patient_id = p_patient_id;
end;
$$;
revoke all on function public.reorder_medications(uuid,uuid[]) from public, anon, authenticated;
grant execute on function public.reorder_medications(uuid,uuid[]) to authenticated;
create index medications_patient_order_idx on public.medications(patient_id,display_order);
notify pgrst, 'reload schema';
commit;
