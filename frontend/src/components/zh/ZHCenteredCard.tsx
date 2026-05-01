export function ZHCenteredCard(props: {
  bgClassName: string;
  cardClassName: string;
  children: React.ReactNode;
}) {
  const { bgClassName, cardClassName, children } = props;
  return (
    <div className={bgClassName}>
      <div className={cardClassName}>{children}</div>
    </div>
  );
}

