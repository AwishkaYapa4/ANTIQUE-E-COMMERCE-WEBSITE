import { useEffect } from 'react';
import { EditorContent, useEditor, type Editor } from '@tiptap/react';
import StarterKit from '@tiptap/starter-kit';
import { Bold, Heading2, Heading3, Italic, Link2, List, ListOrdered, Quote, Redo2, Undo2 } from 'lucide-react';
import { cn } from '@/utils/format';

function ToolbarButton({ onClick, active, label, children }: { onClick: () => void; active?: boolean; label: string; children: React.ReactNode }) {
  return (
    <button type="button" onMouseDown={(e) => e.preventDefault()} onClick={onClick} aria-label={label} aria-pressed={active} title={label}
      className={cn('inline-flex h-8 w-8 items-center justify-center rounded-theme text-ink hover:bg-surface', active && 'bg-surface text-primary')}>
      {children}
    </button>
  );
}

function Toolbar({ editor }: { editor: Editor }) {
  const setLink = () => {
    const previous = editor.getAttributes('link').href as string | undefined;
    const url = window.prompt('Link URL (https://… or /page)', previous ?? '');
    if (url === null) return;
    if (url === '') { editor.chain().focus().unsetLink().run(); return; }
    // Only http(s), mailto, tel or site-relative links; the server sanitizer enforces the same rule.
    if (!/^(https?:\/\/|mailto:|tel:|\/(?!\/))/i.test(url)) { window.alert('Please use a link starting with https://, mailto:, tel: or /'); return; }
    editor.chain().focus().extendMarkRange('link').setLink({ href: url }).run();
  };
  return (
    <div className="flex flex-wrap gap-0.5 border-b border-line bg-bg px-2 py-1.5" role="toolbar" aria-label="Formatting">
      <ToolbarButton label="Bold" active={editor.isActive('bold')} onClick={() => editor.chain().focus().toggleBold().run()}><Bold className="h-4 w-4" /></ToolbarButton>
      <ToolbarButton label="Italic" active={editor.isActive('italic')} onClick={() => editor.chain().focus().toggleItalic().run()}><Italic className="h-4 w-4" /></ToolbarButton>
      <ToolbarButton label="Heading" active={editor.isActive('heading', { level: 2 })} onClick={() => editor.chain().focus().toggleHeading({ level: 2 }).run()}><Heading2 className="h-4 w-4" /></ToolbarButton>
      <ToolbarButton label="Subheading" active={editor.isActive('heading', { level: 3 })} onClick={() => editor.chain().focus().toggleHeading({ level: 3 }).run()}><Heading3 className="h-4 w-4" /></ToolbarButton>
      <ToolbarButton label="Bulleted list" active={editor.isActive('bulletList')} onClick={() => editor.chain().focus().toggleBulletList().run()}><List className="h-4 w-4" /></ToolbarButton>
      <ToolbarButton label="Numbered list" active={editor.isActive('orderedList')} onClick={() => editor.chain().focus().toggleOrderedList().run()}><ListOrdered className="h-4 w-4" /></ToolbarButton>
      <ToolbarButton label="Quote" active={editor.isActive('blockquote')} onClick={() => editor.chain().focus().toggleBlockquote().run()}><Quote className="h-4 w-4" /></ToolbarButton>
      <ToolbarButton label="Link" active={editor.isActive('link')} onClick={setLink}><Link2 className="h-4 w-4" /></ToolbarButton>
      <span className="mx-1 w-px bg-line" aria-hidden />
      <ToolbarButton label="Undo" onClick={() => editor.chain().focus().undo().run()}><Undo2 className="h-4 w-4" /></ToolbarButton>
      <ToolbarButton label="Redo" onClick={() => editor.chain().focus().redo().run()}><Redo2 className="h-4 w-4" /></ToolbarButton>
    </div>
  );
}

/** Minimal rich-text editor. It can only produce structural HTML; there is no way to insert scripts or styles. */
export function RichTextEditor({ value, onChange, minHeight = 180 }: { value: string; onChange: (html: string) => void; minHeight?: number }) {
  const editor = useEditor({
    extensions: [StarterKit.configure({ heading: { levels: [2, 3] }, link: { openOnClick: false, autolink: true, protocols: ['http', 'https', 'mailto', 'tel'] } })],
    content: value,
    editorProps: { attributes: { class: 'prose-antique px-4 py-3 focus:outline-none', style: `min-height:${minHeight}px` } },
    onUpdate: ({ editor: e }) => onChange(e.isEmpty ? '' : e.getHTML()),
  });
  // Sync external value changes (e.g. after loading a product) without fighting the user's typing.
  useEffect(() => {
    // The instance can already be destroyed (e.g. React StrictMode re-mount); never touch it then.
    if (!editor || editor.isDestroyed || editor.isFocused) return;
    if (value !== editor.getHTML()) editor.commands.setContent(value || '', { emitUpdate: false });
  }, [value, editor]);
  if (!editor) return null;
  return (
    <div className="overflow-hidden rounded-theme border border-line bg-paper focus-within:border-accent">
      <Toolbar editor={editor} />
      <EditorContent editor={editor} />
    </div>
  );
}
