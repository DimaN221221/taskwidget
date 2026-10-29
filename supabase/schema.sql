-- Tasks storage for "Задачі на день".
-- Run once in Supabase → SQL Editor → New query → paste → Run.

-- One row per task per user. `data` is the whole task as the app sees it
-- ({ id, text, done, day, o, u, deleted? }); `u` is the time of its last change (ms).
create table if not exists public.tasks (
  user_id    uuid        not null default auth.uid() references auth.users (id) on delete cascade,
  id         text        not null,
  data       jsonb       not null,
  u          bigint      not null,
  updated_at timestamptz not null default now(),
  primary key (user_id, id)
);

create index if not exists tasks_user_updated_at on public.tasks (user_id, updated_at);

-- Each user sees and changes only their own tasks.
alter table public.tasks enable row level security;

drop policy if exists "tasks: read own"   on public.tasks;
drop policy if exists "tasks: add own"    on public.tasks;
drop policy if exists "tasks: change own" on public.tasks;

create policy "tasks: read own"   on public.tasks for select using (auth.uid() = user_id);
create policy "tasks: add own"    on public.tasks for insert with check (auth.uid() = user_id);
create policy "tasks: change own" on public.tasks for update using (auth.uid() = user_id) with check (auth.uid() = user_id);

-- The newest change wins, whatever order devices upload in:
-- an update that is not newer than the stored row is silently skipped.
-- updated_at is the server's clock; apps download rows changed after the last one they saw.
create or replace function public.tasks_keep_newest() returns trigger
language plpgsql as $$
begin
  if tg_op = 'UPDATE' and new.u <= old.u then
    return null;
  end if;
  new.updated_at := clock_timestamp();
  return new;
end
$$;

drop trigger if exists tasks_keep_newest on public.tasks;
create trigger tasks_keep_newest
  before insert or update on public.tasks
  for each row execute function public.tasks_keep_newest();
