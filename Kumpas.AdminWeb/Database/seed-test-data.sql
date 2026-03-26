insert into public.system_logs (id, created_at)
select gen_random_uuid(), now()
where not exists (select 1 from public.system_logs);
