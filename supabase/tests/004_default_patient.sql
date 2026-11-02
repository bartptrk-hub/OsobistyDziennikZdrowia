-- Run in a TEST project after migration 004. All fixtures roll back.
begin;
insert into auth.users(id,email) values
 ('14000000-0000-0000-0000-000000000001','default-a@example.invalid'),
 ('14000000-0000-0000-0000-000000000002','default-b@example.invalid');
insert into public.patients(id,owner_id,name) values
 ('24000000-0000-0000-0000-000000000001','14000000-0000-0000-0000-000000000001','A1'),
 ('24000000-0000-0000-0000-000000000002','14000000-0000-0000-0000-000000000001','A2'),
 ('24000000-0000-0000-0000-000000000003','14000000-0000-0000-0000-000000000002','B');
select set_config('request.jwt.claims','{"sub":"14000000-0000-0000-0000-000000000001","role":"authenticated"}',true);
set local role authenticated;
do $$
begin
  perform public.set_default_patient('24000000-0000-0000-0000-000000000001');
  perform public.set_default_patient('24000000-0000-0000-0000-000000000002');
  if (select count(*) from public.patients where is_default) <> 1 then raise exception 'FAIL: expected exactly one default'; end if;
  if not exists(select 1 from public.patients where id='24000000-0000-0000-0000-000000000002' and is_default) then raise exception 'FAIL: default did not switch'; end if;
  begin
    perform public.set_default_patient('24000000-0000-0000-0000-000000000003');
    raise exception 'FAIL: foreign default selected';
  exception when insufficient_privilege then null; end;
  if not exists(select 1 from public.patients where id='24000000-0000-0000-0000-000000000002' and is_default) then raise exception 'FAIL: rejected operation cleared previous default'; end if;
  begin
    update public.patients set is_default=true where id='24000000-0000-0000-0000-000000000001';
    raise exception 'FAIL: two defaults allowed';
  exception when unique_violation then null; end;
end;
$$;
reset role;
select set_config('request.jwt.claims','{"sub":"14000000-0000-0000-0000-000000000002","role":"authenticated"}',true);
set local role authenticated;
do $$
begin
  if exists(select 1 from public.patients where is_default) then raise exception 'FAIL: default leaked across accounts'; end if;
  perform public.set_default_patient('24000000-0000-0000-0000-000000000003');
  if (select count(*) from public.patients where is_default) <> 1 then raise exception 'FAIL: second account cannot choose own default'; end if;
end;
$$;
reset role;
set local role anon;
do $$ begin
  begin perform public.set_default_patient('24000000-0000-0000-0000-000000000001'); raise exception 'FAIL: anonymous default selection'; exception when insufficient_privilege then null; end;
end; $$;
reset role;
rollback;
select 'PASS: default patient switching, uniqueness and account isolation' as result;
