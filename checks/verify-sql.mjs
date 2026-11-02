// Optional: npm install --prefix .artifacts/sql-checks @electric-sql/pglite --no-audit --no-fund
// Then: node checks/verify-sql.mjs
import { PGlite } from '../.artifacts/sql-checks/node_modules/@electric-sql/pglite/dist/index.js';
import { readFile } from 'node:fs/promises';
const database = new PGlite();
try {
  // A local auth stub only; no Supabase connection or credentials are used.
  await database.exec(`
    create role anon;
    create role authenticated;
    create schema auth;
    create table auth.users(id uuid primary key, email text);
    create function auth.uid() returns uuid language sql stable as $$
      select (nullif(current_setting('request.jwt.claims',true),'')::jsonb ->> 'sub')::uuid
    $$;
    grant usage on schema auth to anon, authenticated;
  `);
  for (const file of ['supabase/migrations/001_initial.sql', 'supabase/migrations/002_history_and_dictionaries.sql', 'supabase/migrations/003_medication_schedule_and_colors.sql', 'supabase/migrations/004_default_patient.sql', 'supabase/tests/access.sql', 'supabase/tests/002_features.sql', 'supabase/tests/003_medications.sql', 'supabase/tests/004_default_patient.sql']) {
    try {
      if (file.includes('003_medication_schedule')) {
        await database.exec(`
          insert into auth.users(id,email) values('99000000-0000-0000-0000-000000000001','upgrade@example.invalid');
          insert into public.patients(id,owner_id,name) values('99000000-0000-0000-0000-000000000002','99000000-0000-0000-0000-000000000001','Upgrade');
          insert into public.medications(id,patient_id,name,dose,unit,start_date,times_of_day) values('99000000-0000-0000-0000-000000000003','99000000-0000-0000-0000-000000000002','Legacy',1,'ml',(now() at time zone 'Europe/Warsaw')::date,array['rano']);
          insert into public.dose_logs(patient_id,medication_id,date,time_of_day) values('99000000-0000-0000-0000-000000000002','99000000-0000-0000-0000-000000000003',(now() at time zone 'Europe/Warsaw')::date,'rano');
        `);
      }
      await database.exec(await readFile(new URL('../' + file, import.meta.url), 'utf8'));
      if (file.includes('003_medication_schedule')) {
        const result = await database.query(`select m.times_of_day[1] as slot,l.time_of_day as logged from public.medications m join public.dose_logs l on l.medication_id=m.id where m.id='99000000-0000-0000-0000-000000000003'`);
        if (result.rows.length !== 1 || result.rows[0].slot !== 'rano (po posiłku)' || result.rows[0].logged !== 'rano (po posiłku)') throw new Error('Morning dose history not preserved by migration');
      }
    } catch (error) {
      console.error(`FAIL ${file}: ${error.message} [${error.code}]\n${error.where ?? ''}`);
      process.exitCode = 1;
      break;
    }
    console.log('PASS ' + file);
  }
  if (!process.exitCode) console.log('Local PostgreSQL checks passed. Supabase Auth and deployed API still need a live integration check.');
} finally {
  await database.close();
}
