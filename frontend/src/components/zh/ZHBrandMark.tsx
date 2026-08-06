interface ZHBrandMarkProps {
  size?: number;
  className?: string;
}

export function ZHBrandMark({
  size = 22,
  className,
}: ZHBrandMarkProps) {
  return (
    <svg
      className={className}
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      aria-hidden="true"
    >
      <path
        d="M8 3.2 4 5.6v4.8l4 2.4 4-2.4V5.6L8 3.2Z"
        stroke="currentColor"
        strokeWidth="1.3"
        strokeLinejoin="round"
      />
      <path
        d="M12 8h3.2M15.2 8v3.2M15.2 11.2H18"
        stroke="currentColor"
        strokeWidth="1.2"
        strokeLinecap="round"
      />
      <circle
        cx="18.2"
        cy="11.2"
        r="1.1"
        fill="currentColor"
      />
    </svg>
  );
}
