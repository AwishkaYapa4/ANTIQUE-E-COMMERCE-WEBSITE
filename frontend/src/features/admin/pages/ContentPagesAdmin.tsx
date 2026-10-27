import { useEffect, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Eye, Plus } from 'lucide-react';
import { toast } from 'sonner';
import { adminApi } from '@/services/adminApi';
import { errorMessage } from '@/services/http';
import type { AdminContentPage } from '@/types/api';
import { Button } from '@/components/ui/Button';
import { Field, Input, Textarea } from '@/components/ui/Field';
import { Badge, ErrorState, PageSpinner } from '@/components/ui/Feedback';
import { Modal, useConfirm } from '@/components/ui/Overlay';
import { formatDateTime } from '@/utils/format';
import { AdminPage, Panel, TableShell, td, th } from '../components/AdminUi';
import { RichTextEditor } from '../components/RichTextEditor';

export function ContentPagesListPage() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [creating, setCreating] = useState(false);
  const [title, setTitle] = useState('');
  const list = useQuery({ queryKey: ['admin', 'pages'], queryFn: adminApi.pages });
  const create = useMutation({
    mutationFn: () => adminApi.createPage({ slug: null, title, body: '<p></p>', seoTitle: null, seoDescription: null }),
    onSuccess: (p) => { queryClient.invalidateQueries({ queryKey: ['admin', 'pages'] }); navigate(`/admin/pages/${p.id}`); },
    onError: (e) => toast.error(errorMessage(e)),
  });
  return (
    <AdminPage title="Content Pages" description="About, contact and policy pages. Edits are drafts until you publish." actions={<Button onClick={() => setCreating(true)}><Plus className="h-4 w-4" /> New page</Button>}>
      <TableShell loading={list.isPending} error={list.error}>
        <table className="w-full">
          <thead className="border-b border-line bg-bg"><tr><th className={th}>Page</th><th className={th}>Status</th><th className={th}>Last published</th></tr></thead>
          <tbody className="divide-y divide-line">
            {list.data?.map((p) => (
              <tr key={p.id} className="hover:bg-bg">
                <td className={td}><Link to={`/admin/pages/${p.id}`} className="font-medium link-underline">{p.draftTitle}</Link><p className="text-xs text-muted">/pages/{p.slug}{p.isSystem && ' · system page'}</p></td>
                <td className={td}><div className="flex gap-1">{p.isPublished ? <Badge tone="success">Published</Badge> : <Badge>Unpublished</Badge>}{p.hasUnpublishedChanges && <Badge tone="warning">Draft changes</Badge>}</div></td>
                <td className={`${td} text-xs text-muted`}>{p.publishedAt ? formatDateTime(p.publishedAt) : '—'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </TableShell>
      <Modal open={creating} onClose={() => setCreating(false)} title="New page" size="sm" footer={<Button loading={create.isPending} disabled={!title.trim()} onClick={() => create.mutate()}>Create</Button>}>
        <Field label="Page title" required>{(p) => <Input {...p} value={title} onChange={(e) => setTitle(e.target.value)} />}</Field>
      </Modal>
    </AdminPage>
  );
}

export function ContentPageEditorPage() {
  const id = Number(useParams().id);
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const confirm = useConfirm();
  const q = useQuery({ queryKey: ['admin', 'pages'], queryFn: adminApi.pages, select: (pages) => pages.find((p) => p.id === id) });
  const [form, setForm] = useState({ title: '', slug: '', body: '', seoTitle: '', seoDescription: '' });
  useEffect(() => {
    if (q.data) setForm({ title: q.data.draftTitle, slug: q.data.slug, body: q.data.draftBody, seoTitle: q.data.seoTitle ?? '', seoDescription: q.data.seoDescription ?? '' });
  }, [q.data]);
  const done = (p: AdminContentPage | void, msg: string) => { queryClient.invalidateQueries({ queryKey: ['admin', 'pages'] }); queryClient.invalidateQueries({ queryKey: ['page'] }); toast.success(msg); return p; };
  const onError = (e: unknown) => toast.error(errorMessage(e));
  const body = () => ({ slug: q.data?.isSystem ? null : form.slug || null, title: form.title, body: form.body, seoTitle: form.seoTitle || null, seoDescription: form.seoDescription || null });
  const save = useMutation({ mutationFn: () => adminApi.savePage(id, body()), onSuccess: (p) => done(p, 'Draft saved'), onError });
  const publish = useMutation({ mutationFn: async () => { await adminApi.savePage(id, body()); return adminApi.publishPage(id); }, onSuccess: (p) => done(p, 'Page published'), onError });
  const unpublish = useMutation({ mutationFn: () => adminApi.unpublishPage(id), onSuccess: (p) => done(p, 'Page unpublished'), onError });
  const discard = useMutation({ mutationFn: () => adminApi.discardPage(id), onSuccess: (p) => done(p, 'Draft discarded'), onError });
  const remove = useMutation({ mutationFn: () => adminApi.deletePage(id), onSuccess: () => { done(undefined, 'Page deleted'); navigate('/admin/pages'); }, onError });

  if (q.isPending) return <PageSpinner />;
  if (!q.data) return <ErrorState message="Page not found." />;
  const p = q.data;
  return (
    <AdminPage title={form.title || 'Untitled page'} description={<>/pages/{p.slug} · {p.isPublished ? 'published' : 'not published'}{p.hasUnpublishedChanges && ' · has draft changes'}</>}
      actions={<>
        <a href={`/pages/${p.slug}?preview=draft`} target="_blank" rel="noopener noreferrer" className="inline-flex h-11 items-center gap-1.5 rounded-theme border border-line px-4 text-sm hover:bg-surface"><Eye className="h-4 w-4" /> Preview draft</a>
        <Button variant="outline" loading={save.isPending} onClick={() => save.mutate()}>Save draft</Button>
        <Button loading={publish.isPending} onClick={() => publish.mutate()}>Publish</Button>
      </>}>
      <div className="grid gap-6 xl:grid-cols-[1fr_320px]">
        <Panel>
          <div className="space-y-4">
            <Field label="Title" required>{(fp) => <Input {...fp} value={form.title} onChange={(e) => setForm({ ...form, title: e.target.value })} />}</Field>
            <RichTextEditor value={form.body} onChange={(html) => setForm((f) => ({ ...f, body: html }))} minHeight={360} />
            <p className="text-xs text-muted">Formatting is limited to headings, lists, quotes and links. Scripts and embedded code are removed automatically for security.</p>
          </div>
        </Panel>
        <div className="space-y-6">
          <Panel title="Page settings">
            <div className="space-y-4">
              <Field label="URL" hint={p.isSystem ? 'System pages keep a fixed URL.' : 'Lowercase letters, numbers and dashes.'}>{(fp) => <Input {...fp} value={form.slug} disabled={p.isSystem} onChange={(e) => setForm({ ...form, slug: e.target.value })} />}</Field>
              <Field label="SEO title">{(fp) => <Input {...fp} value={form.seoTitle} onChange={(e) => setForm({ ...form, seoTitle: e.target.value })} />}</Field>
              <Field label="SEO description">{(fp) => <Textarea {...fp} rows={3} value={form.seoDescription} onChange={(e) => setForm({ ...form, seoDescription: e.target.value })} />}</Field>
            </div>
          </Panel>
          <Panel title="More actions">
            <div className="flex flex-col items-start gap-2">
              {p.hasUnpublishedChanges && <Button variant="ghost" size="sm" onClick={() => discard.mutate()}>Discard draft changes</Button>}
              {!p.isSystem && p.isPublished && <Button variant="ghost" size="sm" onClick={() => unpublish.mutate()}>Unpublish</Button>}
              {!p.isSystem && <Button variant="ghost" size="sm" className="text-danger" onClick={async () => { if (await confirm({ title: 'Delete page?', message: 'Links to this page will stop working.', danger: true, confirmLabel: 'Delete' })) remove.mutate(); }}>Delete page</Button>}
            </div>
          </Panel>
        </div>
      </div>
    </AdminPage>
  );
}
