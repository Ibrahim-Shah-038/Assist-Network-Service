using System.Collections.Generic;
using System.Windows.Forms;

namespace Assist_Service.Helpers
{
    public class PeerSelectionManager
    {
        private HashSet<string> selectedPeers = new HashSet<string>();

        // Expose selected peers
        public IReadOnlyCollection<string> SelectedPeers => selectedPeers;

        // Handle selection toggle
        public void ToggleSelection(PictureBox pcIcon)
        {
            if (pcIcon.Tag is string peerName)
            {
                if (selectedPeers.Contains(peerName))
                {
                    selectedPeers.Remove(peerName);
                    pcIcon.BorderStyle = BorderStyle.None; // unselect
                }
                else
                {
                    selectedPeers.Add(peerName);
                    pcIcon.BorderStyle = BorderStyle.Fixed3D; // select
                }
            }
        }

        // Clear all selections (optional helper)
        public void ClearSelections()
        {
            selectedPeers.Clear();
        }
    }
}
