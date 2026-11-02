-- Run in a TEST project after migration 003. Fixtures are rolled back.
begin;
insert into auth.users(id,email) values
 ('13000000-0000-0000-0000-000000000001','meds-a@example.invalid'),
 ('13000000-0000-0000-0000-000000000002','meds-b@example.invalid');
insert into public.patients(id,owner_id,name) values
 ('23000000-0000-0000-0000-000000000001','13000000-0000-0000-0000-000000000001','A'),
 ('23000000-0000-0000-0000-000000000002','13000000-0000-0000-0000-000000000002','B');
insert into public.medications(id,patient_id,name,dose,unit,start_date,times_of_day,schedule_mode,color) values
 ('43000000-0000-0000-0000-000000000001','23000000-0000-0000-0000-000000000001','Clock',1,'ml',(now() at time zone 'Europe/Warsaw')::date,array['08:00','20:30'],'clock','blue'),
 ('43000000-0000-0000-0000-000000000002','23000000-0000-0000-0000-000000000001','Period',1,'ml',(now() at time zone 'Europe/Warsaw')::date,array['rano (po posiłku)'],'period','rose'),
 ('43000000-0000-0000-0000-000000000003','23000000-0000-0000-0000-000000000002','Foreign',1,'ml',(now() at time zone 'Europe/Warsaw')::date,array['08:00'],'clock','sage');
select set_config('request.jwt.claims','{"sub":"13000000-0000-0000-0000-000000000001","role":"authenticated"}',true);
set local role authenticated;
do $$
declare a uuid := '43000000-0000-0000-0000-000000000001'; b uuid := '43000000-0000-0000-0000-000000000002'; foreign_id uuid := '43000000-0000-0000-0000-000000000003'; p uuid := '23000000-0000-0000-0000-000000000001';
begin
  perform public.reorder_medications(p,array[b,a]);
  if (select display_order from public.medications where id=b) <> 0 or (select display_order from public.medications where id=a) <> 1 then raise exception 'FAIL: order not saved'; end if;
  if (select color from public.medications where id=b) <> 'rose' or (select schedule_mode from public.medications where id=a) <> 'clock' then raise exception 'FAIL: reorder overwrote medication'; end if;
  begin perform public.reorder_medications(p,array[a,foreign_id]); raise exception 'FAIL: foreign id accepted'; exception when check_violation then null; end;
  begin perform public.reorder_medications(p,array[a,a]); raise exception 'FAIL: duplicate id accepted'; exception when check_violation then null; end;
  begin perform public.reorder_medications(p,array[a]); raise exception 'FAIL: incomplete list accepted'; exception when check_violation then null; end;
  begin perform public.reorder_medications('23000000-0000-0000-0000-000000000002',array[foreign_id]); raise exception 'FAIL: foreign patient reorder'; exception when insufficient_privilege then null; end;
  insert into public.dose_logs(patient_id,medication_id,date,time_of_day) values(p,a,(now() at time zone 'Europe/Warsaw')::date,'08:00');
  begin insert into public.dose_logs(patient_id,medication_id,date,time_of_day) values(p,a,(now() at time zone 'Europe/Warsaw')::date,'09:00'); raise exception 'FAIL: unscheduled clock dose'; exception when check_violation then null; end;
  begin update public.medications set times_of_day=array['24:00'] where id=a; raise exception 'FAIL: invalid hour'; exception when check_violation then null; end;
  begin update public.medications set times_of_day=array['8:00'] where id=a; raise exception 'FAIL: noncanonical hour'; exception when check_violation then null; end;
  begin update public.medications set times_of_day=array['08:00','08:00'] where id=a; raise exception 'FAIL: duplicate slot'; exception when check_violation then null; end;
  begin update public.medications set times_of_day=array['rano (po posiłku)','08:00'] where id=a; raise exception 'FAIL: mixed modes'; exception when check_violation then null; end;
  begin update public.medications set color='invalid' where id=a; raise exception 'FAIL: arbitrary color'; exception when check_violation then null; end;
end;
$$;
reset role;
set local role anon;
do $$ begin
  begin perform public.reorder_medications('23000000-0000-0000-0000-000000000001','{}'); raise exception 'FAIL: anonymous reorder'; exception when insufficient_privilege then null; end;
end; $$;
reset role;
rollback;
select 'PASS: persistent order, ownership, colors, clock schedules and dose validation' as result;
