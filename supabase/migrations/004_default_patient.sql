-- Run once after 003. Keeps existing profiles; initially selects the first by name.
begin;
alter table public.patients add column is_default boolean not null default false;
with ranked as (
  select id, row_number() over(partition by owner_id order by name,id) as position from public.patients
) update public.patients p set is_default = true from ranked r where r.id = p.id and r.position = 1;
create unique index patients_one_default_per_owner on public.patients(owner_id) where is_default;

create function public.set_default_patient(p_patient_id uuid) returns void
language plpgsql security invoker set search_path = '' as $$
begin
  -- Always lock in the same order to serialize two selections on this account.
  perform p.id from public.patients p where p.owner_id = auth.uid() order by p.id for update;
  if not exists(select 1 from public.patients where id = p_patient_id and owner_id = auth.uid()) then
    raise exception 'Patient not accessible' using errcode = '42501';
  end if;
  update public.patients set is_default = false where owner_id = auth.uid() and is_default;
  update public.patients set is_default = true where id = p_patient_id and owner_id = auth.uid();
end;
$$;
revoke all on function public.set_default_patient(uuid) from public, anon, authenticated;
grant execute on function public.set_default_patient(uuid) to authenticated;
notify pgrst, 'reload schema';
commit;
