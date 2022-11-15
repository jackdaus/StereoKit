using StereoKit;

class DemoFBSpatialEntity : ITest
{
	string title       = "FB Spatial Entity Extension";
	string description = "World locked entities!";
	Pose   windowPose  = Demo.contentPose * Pose.Identity;

	public void Initialize() { }
	public void Shutdown() { }

	public void Update()
	{
		UI.WindowBegin("Spatial Entity Settings", ref windowPose);

		if (!App.fbSpatialEntity.Available)
		{
			UI.Label("No FB Spatial Entity EXT available :(");
		}
		else
		{
			UI.Label("FB Spatial Entity EXT available!");
			if (UI.Button("Create Anchor"))
			{
				// Create an anchor at pose of the right index finger tip
				Pose fingerPose = Input.Hand(Handed.Right)[FingerId.Index, JointId.Tip].Pose;
				App.fbSpatialEntity.CreateAnchor(fingerPose);
			}
			if (UI.Button("Load Anchors"))
			{
				App.fbSpatialEntity.LoadAnchors();
			}
			if (UI.Button("Erase All Anchors"))
			{
				App.fbSpatialEntity.EraseAnchors();
			}
		}

		UI.WindowEnd();

		// Spatial anchor visual
		App.fbSpatialEntity.Anchors.ForEach(anchor =>
		{
			Mesh.Cube.Draw(Material.Default, anchor.pose.ToMatrix(0.2f), new Color(1, 0.5f, 0));
		});

		Demo.ShowSummary(title, description);
	}
}