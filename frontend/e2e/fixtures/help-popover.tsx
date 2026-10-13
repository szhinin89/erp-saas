import { useRef, useState } from "react";
import { createRoot, type Root } from "react-dom/client";
import { ZHHelpPopover, ZHSectionHelp, ZHFieldHelp } from "../../src/components/zh/help";
import { HELP_KEYS } from "../../src/help";
import "../../src/index.css";
import "../../src/components/zh/ZHAppTenantHeader.css";
function Fixture() {
  const anchorRef = useRef<HTMLButtonElement>(null);
  const [open, setOpen] = useState(false);
  return <>
    <div id="ancestor" style={{ overflow: "hidden", transform: "translateZ(0)", width: "100%", height: "100vh" }}>
      <button id="anchor" ref={anchorRef} style={{ position: "absolute", left: "50%", top: 200 }} onClick={() => setOpen(true)}>Open</button>
      <div id="consumers" style={{ position: "absolute", right: 16, top: 80 }}>
        <ZHSectionHelp helpKey={HELP_KEYS.PURCHASES_RECEPTION_OVERVIEW} />
        <ZHSectionHelp helpKey={HELP_KEYS.SALES_CUSTOMER_CONSUMER_FINAL} variables={{ maxConsumerFinalAmount: "50" }} />
        <ZHFieldHelp helpKey={HELP_KEYS.PURCHASES_SUPPLIER} />
        <ZHFieldHelp helpKey={HELP_KEYS.SALES_PAYMENTS_SECTION} />
      </div>
    </div>
    <ZHHelpPopover open={open} onClose={() => setOpen(false)} anchorRef={anchorRef} titleId="test-title" title="Help">
      <p id="content">Real help geometry with wrapping text and a long value: {"x".repeat(100)}</p>
    </ZHHelpPopover>
  </>;
}
// Vite puede reevaluar el fixture con Fast Refresh: conservar su raíz de React.
const mount = document.getElementById("root")! as HTMLElement & { helpTestRoot?: Root };
mount.helpTestRoot ??= createRoot(mount);
mount.helpTestRoot.render(<Fixture />);
