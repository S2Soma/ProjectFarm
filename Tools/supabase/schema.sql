-- MATU Farm — bảng lưu tiến trình trên Supabase.
--
-- Chạy MỘT LẦN: Supabase Dashboard ▸ SQL Editor ▸ New query ▸ dán cả file ▸ Run.
-- Chạy lại cũng không sao (mọi lệnh đều "if not exists" / "or replace" / drop policy trước).
--
-- Bảo mật: game chỉ cầm publishable (anon) key. Mọi quyền đọc/ghi đi qua Row Level Security:
-- một người chơi chỉ thấy và sửa được đúng dòng có id của chính mình (auth.uid()).
-- Tài khoản khách (anonymous sign-in) cũng là role "authenticated" nên dùng chung các policy này.

-- =====================================================================
-- saves: một dòng cho mỗi tài khoản, chứa nguyên file lưu của game
-- =====================================================================
create table if not exists public.saves (
  user_id      uuid primary key references auth.users (id) on delete cascade,
  data         jsonb  not null,                 -- nguyên nội dung lqfarm.save.json
  level        int    not null default 1,       -- tách ra để xem nhanh, không cần mở data
  coin         bigint not null default 0,
  save_version int    not null default 2,
  saved_at     bigint not null default 0,       -- unix ms do máy ghi; game dùng làm khoá chống ghi đè
  device       text,                            -- mã ngẫu nhiên của máy ghi lần cuối
  updated_at   timestamptz not null default now(),
  constraint saves_data_is_object check (jsonb_typeof(data) = 'object'),
  constraint saves_data_size check (pg_column_size(data) < 2000000)
);

-- =====================================================================
-- profiles: tên hiển thị + cấp, cho bạn bè / bảng xếp hạng sau này
-- =====================================================================
create table if not exists public.profiles (
  id           uuid primary key references auth.users (id) on delete cascade,
  display_name text not null default 'Nông dân',
  level        int  not null default 1,
  updated_at   timestamptz not null default now(),
  constraint profiles_name_len check (char_length(display_name) between 1 and 40)
);

-- updated_at do máy chủ đóng dấu, không tin máy người chơi
create or replace function public.mitfarm_touch_updated_at()
returns trigger language plpgsql as $$
begin
  new.updated_at := now();
  return new;
end $$;

drop trigger if exists saves_touch on public.saves;
create trigger saves_touch before insert or update on public.saves
  for each row execute function public.mitfarm_touch_updated_at();

drop trigger if exists profiles_touch on public.profiles;
create trigger profiles_touch before insert or update on public.profiles
  for each row execute function public.mitfarm_touch_updated_at();

-- =====================================================================
-- Row Level Security
-- =====================================================================
alter table public.saves    enable row level security;
alter table public.profiles enable row level security;

drop policy if exists "saves: read own"   on public.saves;
drop policy if exists "saves: insert own" on public.saves;
drop policy if exists "saves: update own" on public.saves;
create policy "saves: read own"   on public.saves for select to authenticated using ((select auth.uid()) = user_id);
create policy "saves: insert own" on public.saves for insert to authenticated with check ((select auth.uid()) = user_id);
create policy "saves: update own" on public.saves for update to authenticated
  using ((select auth.uid()) = user_id) with check ((select auth.uid()) = user_id);

drop policy if exists "profiles: read own"   on public.profiles;
drop policy if exists "profiles: insert own" on public.profiles;
drop policy if exists "profiles: update own" on public.profiles;
create policy "profiles: read own"   on public.profiles for select to authenticated using ((select auth.uid()) = id);
create policy "profiles: insert own" on public.profiles for insert to authenticated with check ((select auth.uid()) = id);
create policy "profiles: update own" on public.profiles for update to authenticated
  using ((select auth.uid()) = id) with check ((select auth.uid()) = id);

-- Không cho xoá từ game; khách (chưa đăng nhập) không đụng được gì.
revoke all on public.saves    from anon;
revoke all on public.profiles from anon;
grant select, insert, update on public.saves    to authenticated;
grant select, insert, update on public.profiles to authenticated;
