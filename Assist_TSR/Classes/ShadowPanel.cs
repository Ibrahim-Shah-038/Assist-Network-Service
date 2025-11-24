using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Assist_TSR.Classes
{
    public class ShadowPanel : Panel
    {
        public int ShadowSize { get; set; } = 8;
        public Color ShadowColor { get; set; } = Color.FromArgb(60, 0, 0, 0);

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Rectangle shadowRect = new Rectangle(
                ShadowSize, ShadowSize,
                Width - ShadowSize,
                Height - ShadowSize
            );

            using (SolidBrush b = new SolidBrush(ShadowColor))
                e.Graphics.FillRectangle(b, shadowRect);
        }
    }
}
