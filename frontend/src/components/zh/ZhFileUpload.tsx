import { useRef, useState } from "react";
import "./zh-file-upload.css";

export type ZhFileUploadCurrentFile = {
  name: string;
  sizeBytes: number;
  uploadedAt: string;
};

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

/**
 * Selector de archivo reutilizable (click o drag&drop) con estado de carga, progreso y
 * metadata del archivo ya guardado. La validación de tipo/tamaño la decide el caller
 * (pasa `error`) — este componente solo administra la interacción de selección.
 */
export function ZhFileUpload(props: {
  accept: string;
  onFileSelected: (file: File) => void;
  disabled?: boolean;
  uploading?: boolean;
  progress?: number;
  error?: string | null;
  currentFile?: ZhFileUploadCurrentFile | null;
  selectLabel: string;
  dropLabel: string;
  uploadingLabel?: string;
  noFileLabel: string;
}) {
  const {
    accept,
    onFileSelected,
    disabled,
    uploading,
    progress = 0,
    error,
    currentFile,
    selectLabel,
    dropLabel,
    uploadingLabel,
    noFileLabel,
  } = props;

  const inputRef = useRef<HTMLInputElement>(null);
  const [dragOver, setDragOver] = useState(false);

  const pick = (file: File | undefined) => {
    if (!file) return;
    onFileSelected(file);
    if (inputRef.current) inputRef.current.value = "";
  };

  return (
    <div className="zh-upload">
      <div
        className={`zh-upload-drop ${dragOver ? "zh-upload-drop--active" : ""} ${disabled ? "zh-upload-drop--disabled" : ""}`}
        onClick={() => !disabled && !uploading && inputRef.current?.click()}
        onDragOver={(e) => {
          e.preventDefault();
          if (!disabled) setDragOver(true);
        }}
        onDragLeave={() => setDragOver(false)}
        onDrop={(e) => {
          e.preventDefault();
          setDragOver(false);
          if (!disabled && !uploading) pick(e.dataTransfer.files?.[0]);
        }}
      >
        <span className="material-symbols-outlined zh-upload-icon">
          upload_file
        </span>
        <p className="zh-upload-select">{selectLabel}</p>
        <p className="zh-upload-hint">{dropLabel}</p>
        <input
          ref={inputRef}
          type="file"
          accept={accept}
          className="zh-upload-input"
          disabled={disabled || uploading}
          onChange={(e) => pick(e.target.files?.[0])}
        />
      </div>

      {uploading && (
        <div className="zh-upload-progress-wrap">
          <div className="zh-upload-progress-bar">
            <div
              className="zh-upload-progress-fill"
              style={{ "--upload-progress": `${progress}%` } as React.CSSProperties}
            />
          </div>
          {uploadingLabel && <p className="zh-upload-hint">{uploadingLabel}</p>}
        </div>
      )}

      {error && <p className="zh-upload-error">{error}</p>}

      {!uploading && currentFile && (
        <div className="zh-upload-current">
          <span className="material-symbols-outlined zh-upload-current-icon">
            description
          </span>
          <div>
            <p className="zh-upload-current-name">{currentFile.name}</p>
            <p className="zh-upload-current-meta">
              {formatBytes(currentFile.sizeBytes)} · {currentFile.uploadedAt}
            </p>
          </div>
        </div>
      )}

      {!uploading && !currentFile && !error && (
        <p className="zh-upload-hint">{noFileLabel}</p>
      )}
    </div>
  );
}
