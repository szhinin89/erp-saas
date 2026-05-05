import { ZHFormHeader } from './ZHForm';

export function ZHModalHeader(props: {
  title: string;
  subtitle: string;
  onClose: () => void;
  closeLabel?: string;
  right?: React.ReactNode;
}) {
  const { title, subtitle, onClose, closeLabel, right } = props;
  return (
    <div className="zh-relative">
      <ZHFormHeader title={title} subtitle={subtitle} right={right} showZhBrandLogo={false} />
      <button className="zh-form-header-close" onClick={onClose} type="button" aria-label={closeLabel ?? 'Close'}>
        ✕
      </button>
    </div>
  );
}

