-- Run after 001_initial.sql in a TEST Supabase project's SQL Editor.
-- No pgTAP dependency. Any failed assertion raises an exception.
-- Test data and users are rolled back at the end.
begin;
insert into auth.users (id, email) values
 ('10000000-0000-0000-0000-000000000001', 'rls-a@example.invalid'),
 ('10000000-0000-0000-0000-000000000002', 'rls-b@example.invalid');
insert into public.patients(id, owner_id, name) values
 ('20000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000001','A'),
 ('20000000-0000-0000-0000-000000000002','10000000-0000-0000-0000-000000000002','B'),
 ('20000000-0000-0000-0000-000000000003','10000000-0000-0000-0000-000000000001','A2');
insert into public.visits(id,patient_id,date,doctor) values
 ('30000000-0000-0000-0000-000000000001','20000000-0000-0000-0000-000000000001',current_date,'A doctor'),
 ('30000000-0000-0000-0000-000000000002','20000000-0000-0000-0000-000000000002',current_date,'B doctor');

set local role anon;
do $$
begin
  begin
    perform * from public.patients;
    raise exception 'FAIL: anon can select patients';
  exception when insufficient_privilege then null;
  end;
end;
$$;
reset role;

select set_config('request.jwt.claims', '{"sub":"10000000-0000-0000-0000-000000000001","role":"authenticated"}', true);
set local role authenticated;
do $$
declare affected integer;
begin
  if (select count(*) from public.patients) <> 2 then raise exception 'FAIL: patient isolation'; end if;
  if (select count(*) from public.visits) <> 1 then raise exception 'FAIL: visit isolation'; end if;
  update public.visits set doctor = 'changed' where id = '30000000-0000-0000-0000-000000000002';
  get diagnostics affected = row_count;
  if affected <> 0 then raise exception 'FAIL: foreign visit update'; end if;
  delete from public.visits where id = '30000000-0000-0000-0000-000000000002';
  get diagnostics affected = row_count;
  if affected <> 0 then raise exception 'FAIL: foreign visit delete'; end if;
  begin
    insert into public.symptoms(patient_id,name,start_date) values ('20000000-0000-0000-0000-000000000002','forbidden',current_date);
    raise exception 'FAIL: foreign symptom insert';
  exception when insufficient_privilege then null;
  end;
  begin
    update public.patients set owner_id = '10000000-0000-0000-0000-000000000002' where id = '20000000-0000-0000-0000-000000000001';
    raise exception 'FAIL: owner reassignment';
  exception when insufficient_privilege then null;
  end;
  begin
    insert into public.medications(patient_id,name,dose,unit,start_date,times_of_day,visit_id)
      values ('20000000-0000-0000-0000-000000000003','cross patient',1,'ml',current_date,array['rano (po posiłku)'],'30000000-0000-0000-0000-000000000001');
    raise exception 'FAIL: cross-patient medication link';
  exception when foreign_key_violation or restrict_violation then null;
  end;
  insert into public.symptoms(patient_id,name,start_date) values ('20000000-0000-0000-0000-000000000001','allowed',current_date);
  if (select count(*) from public.symptoms) <> 1 then raise exception 'FAIL: own symptom insert'; end if;
  insert into public.medications(id,patient_id,name,dose,unit,start_date,times_of_day)
    values ('40000000-0000-0000-0000-000000000001','20000000-0000-0000-0000-000000000001','allowed',1,'ml',(now() at time zone 'Europe/Warsaw')::date,array['rano (po posiłku)']);
  insert into public.dose_logs(patient_id,medication_id,date,time_of_day)
    values ('20000000-0000-0000-0000-000000000001','40000000-0000-0000-0000-000000000001',(now() at time zone 'Europe/Warsaw')::date,'rano (po posiłku)');
  begin
    insert into public.dose_logs(patient_id,medication_id,date,time_of_day)
      values ('20000000-0000-0000-0000-000000000001','40000000-0000-0000-0000-000000000001',(now() at time zone 'Europe/Warsaw')::date,'rano (po posiłku)');
    raise exception 'FAIL: duplicate dose';
  exception when unique_violation then null;
  end;
  begin
    insert into public.dose_logs(patient_id,medication_id,date,time_of_day)
      values ('20000000-0000-0000-0000-000000000001','40000000-0000-0000-0000-000000000001',(now() at time zone 'Europe/Warsaw')::date,'przed snem');
    raise exception 'FAIL: invalid dose schedule';
  exception when check_violation then null;
  end;
  begin
    delete from public.medications where id = '40000000-0000-0000-0000-000000000001';
    raise exception 'FAIL: lost dose history';
  exception when foreign_key_violation or restrict_violation then null;
  end;
end;
$$;
reset role;
select set_config('request.jwt.claims', '{"sub":"10000000-0000-0000-0000-000000000002","role":"authenticated"}', true);
set local role authenticated;
do $$
begin
  if (select count(*) from public.patients) <> 1 then raise exception 'FAIL: account B isolation'; end if;
  if (select count(*) from public.symptoms) <> 0 then raise exception 'FAIL: foreign symptom visibility'; end if;
  if (select count(*) from public.medications) <> 0 then raise exception 'FAIL: foreign medication visibility'; end if;
  if (select count(*) from public.dose_logs) <> 0 then raise exception 'FAIL: foreign dose visibility'; end if;
end;
$$;
reset role;
rollback;
select 'PASS: RLS isolation, relationship constraints and dose history checks' as result;
