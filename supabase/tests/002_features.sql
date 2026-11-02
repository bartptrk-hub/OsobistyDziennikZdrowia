-- Run in a TEST project after migrations 001 and 002. All fixtures roll back.
begin;
insert into auth.users(id,email) values
 ('11000000-0000-0000-0000-000000000001','features-a@example.invalid'),
 ('11000000-0000-0000-0000-000000000002','features-b@example.invalid');
insert into public.patients(id,owner_id,name) values
 ('22000000-0000-0000-0000-000000000001','11000000-0000-0000-0000-000000000001','A'),
 ('22000000-0000-0000-0000-000000000002','11000000-0000-0000-0000-000000000002','B');
insert into public.visits(patient_id,date,doctor,clinic) values
 ('22000000-0000-0000-0000-000000000001','2026-01-01','Lekarz historyczny','Placówka historyczna');
insert into public.medications(id,patient_id,name,dose,unit,start_date,times_of_day) values
 ('44000000-0000-0000-0000-000000000001','22000000-0000-0000-0000-000000000001','Lek historyczny',1,'mg','2026-01-01',array['rano (po posiłku)']);
insert into public.symptoms(id,patient_id,name,area,start_date) values
 ('33000000-0000-0000-0000-000000000001','22000000-0000-0000-0000-000000000001','Katar','Głowa/Szyja','2026-01-01'),
 ('33000000-0000-0000-0000-000000000002','22000000-0000-0000-0000-000000000002','Cudzy objaw','','2026-01-01'),
 ('33000000-0000-0000-0000-000000000003','22000000-0000-0000-0000-000000000001','Ból gardła','','2026-01-01');
insert into public.dictionary_entries(owner_id,kind,value) values ('11000000-0000-0000-0000-000000000002','doctor','Cudzy lekarz');

select set_config('request.jwt.claims','{"sub":"11000000-0000-0000-0000-000000000001","role":"authenticated"}',true);
set local role authenticated;
do $$
declare payload jsonb; result public.symptoms; affected integer;
begin
  perform public.initialize_dictionaries();
  perform public.initialize_dictionaries();
  if (select count(*) from public.dictionary_entries where kind = 'medication' and value = 'Lek historyczny') <> 1 then raise exception 'FAIL: dictionary history import/idempotency'; end if;
  if not exists(select 1 from public.dictionary_entries where kind = 'doctor' and value = 'Lekarz historyczny') then raise exception 'FAIL: imported doctor'; end if;
  if not exists(select 1 from public.dictionary_entries where kind = 'clinic' and value = 'Placówka historyczna') then raise exception 'FAIL: imported clinic'; end if;
  if exists(select 1 from public.dictionary_entries where value = 'Cudzy lekarz') then raise exception 'FAIL: dictionary isolation'; end if;
  delete from public.dictionary_entries where kind = 'unit' and value = 'ml';
  perform public.initialize_dictionaries();
  if exists(select 1 from public.dictionary_entries where kind = 'unit' and value = 'ml') then raise exception 'FAIL: deleted default recreated'; end if;
  update public.dictionary_entries set value = 'krople' where kind = 'unit' and value = 'mg';
  if (select unit from public.medications where id = '44000000-0000-0000-0000-000000000001') <> 'mg' then raise exception 'FAIL: historical dosage rewritten'; end if;
  update public.medications set unit = 'krople' where id = '44000000-0000-0000-0000-000000000001';
  begin
    insert into public.dictionary_entries(kind,value) values ('unit','KROPLE');
    raise exception 'FAIL: duplicate dictionary value';
  exception when unique_violation then null; end;
  begin
    insert into public.dictionary_entries(owner_id,kind,value) values ('11000000-0000-0000-0000-000000000002','unit','forbidden');
    raise exception 'FAIL: foreign dictionary insert';
  exception when insufficient_privilege then null; end;
  update public.dictionary_entries set value = 'forbidden' where owner_id = '11000000-0000-0000-0000-000000000002';
  get diagnostics affected = row_count;
  if affected <> 0 then raise exception 'FAIL: foreign dictionary update'; end if;
  delete from public.dictionary_entries where owner_id = '11000000-0000-0000-0000-000000000002';
  get diagnostics affected = row_count;
  if affected <> 0 then raise exception 'FAIL: foreign dictionary delete'; end if;

  payload := jsonb_build_object('id','55000000-0000-0000-0000-000000000001','patient_id','22000000-0000-0000-0000-000000000001','origin_id','33000000-0000-0000-0000-000000000001','name','Kaszel','area','Układ oddechowy','description','Powiązany objaw','start_date','2026-01-02','end_date',null);
  result := public.transition_symptom(payload,true);
  if result.origin_id <> '33000000-0000-0000-0000-000000000001' then raise exception 'FAIL: transition origin'; end if;
  if (select end_date from public.symptoms where id = result.origin_id) <> '2026-01-02'::date then raise exception 'FAIL: source closure'; end if;
  perform public.transition_symptom(payload,true);
  if (select count(*) from public.symptoms where origin_id = result.origin_id) <> 1 then raise exception 'FAIL: transition retry duplicated'; end if;
  begin
    update public.symptoms set start_date = '2026-01-03' where id = result.origin_id;
    raise exception 'FAIL: chronology after editing source';
  exception when check_violation then null; end;
  begin
    update public.symptoms set origin_id = null where id = result.id;
    raise exception 'FAIL: origin reassignment';
  exception when check_violation then null; end;
  begin
    delete from public.symptoms where id = result.origin_id;
    raise exception 'FAIL: deleted source with children';
  exception when foreign_key_violation or restrict_violation then null; end;

  payload := payload || jsonb_build_object('id','55000000-0000-0000-0000-000000000002','origin_id','33000000-0000-0000-0000-000000000003');
  perform public.transition_symptom(payload,false);
  if (select end_date from public.symptoms where id = '33000000-0000-0000-0000-000000000003') is not null then raise exception 'FAIL: continuing source ended'; end if;
  begin
    perform public.transition_symptom(payload || jsonb_build_object('id','55000000-0000-0000-0000-000000000003','start_date','2025-12-31'),true);
    raise exception 'FAIL: transition before source';
  exception when check_violation then null; end;
  if (select end_date from public.symptoms where id = '33000000-0000-0000-0000-000000000003') is not null then raise exception 'FAIL: invalid transition partially ended source'; end if;
  if exists(select 1 from public.symptoms where id = '55000000-0000-0000-0000-000000000003') then raise exception 'FAIL: invalid transition partially inserted child'; end if;
  begin
    perform public.transition_symptom(payload || jsonb_build_object('id','55000000-0000-0000-0000-000000000004','origin_id','33000000-0000-0000-0000-000000000002'),true);
    raise exception 'FAIL: foreign source accessible';
  exception when insufficient_privilege then null; end;
end;
$$;
reset role;
select set_config('request.jwt.claims','{"sub":"11000000-0000-0000-0000-000000000002","role":"authenticated"}',true);
set local role authenticated;
do $$
begin
  if (select count(*) from public.dictionary_entries) <> 1 then raise exception 'FAIL: reverse dictionary isolation'; end if;
  if exists(select 1 from public.symptoms where origin_id is not null) then raise exception 'FAIL: linked symptom isolation'; end if;
end;
$$;
reset role;
set local role anon;
do $$
begin
  begin perform * from public.dictionary_entries; raise exception 'FAIL: anonymous dictionary read'; exception when insufficient_privilege then null; end;
  begin perform public.initialize_dictionaries(); raise exception 'FAIL: anonymous dictionary RPC'; exception when insufficient_privilege then null; end;
  begin perform public.transition_symptom('{}',true); raise exception 'FAIL: anonymous transition RPC'; exception when insufficient_privilege then null; end;
end;
$$;
reset role;
rollback;
select 'PASS: dictionary CRUD/RLS, history preservation, atomic symptom links and chronology' as result;
