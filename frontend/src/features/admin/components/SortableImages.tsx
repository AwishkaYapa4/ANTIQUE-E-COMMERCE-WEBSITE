import { DndContext, closestCenter, KeyboardSensor, PointerSensor, useSensor, useSensors, type DragEndEvent } from '@dnd-kit/core';
import { arrayMove, rectSortingStrategy, SortableContext, sortableKeyboardCoordinates, useSortable } from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';
import { GripVertical, X } from 'lucide-react';
import { Input } from '@/components/ui/Field';

export interface EditableImage {
  key: string;
  mediaAssetId: number | null;
  url: string;
  altText: string;
}

function SortableImage({ image, index, onRemove, onAlt }: { image: EditableImage; index: number; onRemove: () => void; onAlt: (v: string) => void }) {
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({ id: image.key });
  return (
    <li ref={setNodeRef} style={{ transform: CSS.Transform.toString(transform), transition }} className={`rounded-theme border bg-paper p-2 ${isDragging ? 'z-10 border-accent shadow-lg' : 'border-line'}`}>
      <div className="relative">
        <img src={image.url} alt={image.altText} className="aspect-[4/5] w-full rounded-theme object-cover" />
        {index === 0 && <span className="absolute top-1.5 left-1.5 rounded-theme bg-primary px-1.5 py-0.5 text-[0.65rem] text-paper">Main</span>}
        <button type="button" onClick={onRemove} className="absolute top-1.5 right-1.5 rounded-full bg-paper/90 p-1 hover:text-danger" aria-label="Remove image"><X className="h-3.5 w-3.5" /></button>
        <button type="button" className="absolute right-1.5 bottom-1.5 cursor-grab rounded-full bg-paper/90 p-1 active:cursor-grabbing" aria-label={`Drag to reorder image ${index + 1}`} {...attributes} {...listeners}>
          <GripVertical className="h-3.5 w-3.5" />
        </button>
      </div>
      <label className="sr-only" htmlFor={`alt-${image.key}`}>Alt text for image {index + 1}</label>
      <Input id={`alt-${image.key}`} value={image.altText} onChange={(e) => onAlt(e.target.value)} placeholder="Describe the image" className="mt-2 px-2 py-1.5 text-xs" maxLength={300} />
    </li>
  );
}

/** Drag-and-drop (mouse, touch and keyboard) image ordering. The first image is the main photo. */
export function SortableImages({ images, onChange }: { images: EditableImage[]; onChange: (images: EditableImage[]) => void }) {
  const sensors = useSensors(useSensor(PointerSensor, { activationConstraint: { distance: 4 } }), useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }));
  const onDragEnd = (e: DragEndEvent) => {
    if (!e.over || e.active.id === e.over.id) return;
    const from = images.findIndex((i) => i.key === e.active.id);
    const to = images.findIndex((i) => i.key === e.over!.id);
    onChange(arrayMove(images, from, to));
  };
  return (
    <DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={onDragEnd}>
      <SortableContext items={images.map((i) => i.key)} strategy={rectSortingStrategy}>
        <ul className="grid grid-cols-2 gap-3 sm:grid-cols-4 lg:grid-cols-5">
          {images.map((img, i) => (
            <SortableImage key={img.key} image={img} index={i}
              onRemove={() => onChange(images.filter((x) => x.key !== img.key))}
              onAlt={(v) => onChange(images.map((x) => (x.key === img.key ? { ...x, altText: v } : x)))} />
          ))}
        </ul>
      </SortableContext>
    </DndContext>
  );
}
