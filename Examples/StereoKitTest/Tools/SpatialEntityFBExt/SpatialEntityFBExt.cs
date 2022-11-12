using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using StereoKit;
using StereoKit.Framework;

using XrSpace = System.UInt64;
using XrSession = System.UInt64;
using XrAsyncRequestIdFB = System.UInt64;

namespace StereoKitTest.Tools.SpatialEntityFBExt
{
	class SpatialEntityFBExt : IStepper
	{
		bool extAvailable;
		bool enabled;

		public List<Anchor> Anchors = new List<Anchor>();

		public class Anchor
		{
			public Guid uuid;
			public XrSpace xrSpace;
			public Pose pose;
			public XrSession requestId;
		}

		public bool Available => extAvailable;
		public bool Enabled { get => extAvailable && enabled; set => enabled = value; }

		public SpatialEntityFBExt() : this(true) { }
		public SpatialEntityFBExt(bool enabled = true)
		{
			if (SK.IsInitialized)
				Log.Err("SpatialEntityFBExt must be constructed before StereoKit is initialized!");
			Backend.OpenXR.RequestExt("XR_FB_spatial_entity");
		}

		public bool Initialize()
		{
			extAvailable =
				Backend.XRType == BackendXRType.OpenXR &&
				Backend.OpenXR.ExtEnabled("XR_FB_spatial_entity") &&
				LoadBindings();

			// Set up xrPollEvent subscription
			if (extAvailable)
			{
				Backend.OpenXR.OnPollEvent += pollEventHandler;
			}

			return true;
		}

		public void Step()
		{
			Anchors.ForEach(a =>
			{
				XrSpaceLocation spaceLocation = new XrSpaceLocation { type = XrStructureType.XR_TYPE_SPACE_LOCATION };

				// TODO consider using XrFrameState.predictedDisplayTime for XrTime argument
				XrResult result = xrLocateSpace(a.xrSpace, Backend.OpenXR.Space, Backend.OpenXR.Time, out spaceLocation);
				if (result == XrResult.Success)
				{
					var orientationValid = spaceLocation.locationFlags.HasFlag(XrSpaceLocationFlags.XR_SPACE_LOCATION_ORIENTATION_VALID_BIT);
					var poseValid        = spaceLocation.locationFlags.HasFlag(XrSpaceLocationFlags.XR_SPACE_LOCATION_POSITION_VALID_BIT);
					if (orientationValid && poseValid)
					{
						a.pose = spaceLocation.pose;
					}
				}
			});
		}

		public void Shutdown()
		{
			// TODO cleanup anchors?
		}

		public bool CreateAnchor(Pose pose)
		{
			Log.Info("Begin CreateAnchor");

			XrSpatialAnchorCreateInfoFB anchorCreateInfo = new XrSpatialAnchorCreateInfoFB(Backend.OpenXR.Space, pose, Backend.OpenXR.Time);

			XrResult result = xrCreateSpatialAnchorFB(
				Backend.OpenXR.Session,
				anchorCreateInfo,
				out XrAsyncRequestIdFB requestId);

			if (result != XrResult.Success)
			{
				Log.Err($"Error creating spatial anchor: {result}");
			}

			Log.Info($"xrCreateSpatialAnchorFB initiated. The request id is: {requestId}. Result: {result}");

			return result == XrResult.Success;
		}


		// XR_FB_spatial_entity New Functions
		del_xrCreateSpatialAnchorFB xrCreateSpatialAnchorFB;
		del_xrGetSpaceUuidFB xrGetSpaceUuidFB;
		del_xrEnumerateSpaceSupportedComponentsFB xrEnumerateSpaceSupportedComponentsFB;
		del_xrSetSpaceComponentStatusFB xrSetSpaceComponentStatusFB;
		del_xrGetSpaceComponentStatusFB xrGetSpaceComponentStatusFB;

		// Misc New Functions
		del_xrLocateSpace xrLocateSpace;

		// XR_FB_spatial_entity
		bool LoadBindings()
		{
			//XR_FB_spatial_entity
			xrCreateSpatialAnchorFB = Backend.OpenXR.GetFunction<del_xrCreateSpatialAnchorFB>("xrCreateSpatialAnchorFB");
			xrGetSpaceUuidFB = Backend.OpenXR.GetFunction<del_xrGetSpaceUuidFB>("xrGetSpaceUuidFB");
			xrEnumerateSpaceSupportedComponentsFB = Backend.OpenXR.GetFunction<del_xrEnumerateSpaceSupportedComponentsFB>("xrEnumerateSpaceSupportedComponentsFB");
			xrSetSpaceComponentStatusFB = Backend.OpenXR.GetFunction<del_xrSetSpaceComponentStatusFB>("xrSetSpaceComponentStatusFB");
			xrGetSpaceComponentStatusFB = Backend.OpenXR.GetFunction<del_xrGetSpaceComponentStatusFB>("xrGetSpaceComponentStatusFB");

			// Misc functions
			xrLocateSpace = Backend.OpenXR.GetFunction<del_xrLocateSpace>("xrLocateSpace");

			return xrCreateSpatialAnchorFB != null
				&& xrGetSpaceUuidFB != null
				&& xrEnumerateSpaceSupportedComponentsFB != null
				&& xrSetSpaceComponentStatusFB != null
				&& xrGetSpaceComponentStatusFB != null
				&& xrLocateSpace != null;
		}

		void pollEventHandler(IntPtr XrEventDataBufferData)
		{
			XrEventDataBuffer myBuffer = Marshal.PtrToStructure<XrEventDataBuffer>(XrEventDataBufferData);
			Log.Info($"xrPollEvent: received {myBuffer.type}");

			switch (myBuffer.type)
			{
				case XrStructureType.XR_TYPE_EVENT_DATA_SPATIAL_ANCHOR_CREATE_COMPLETE_FB:
					XrEventDataSpatialAnchorCreateCompleteFB spatialAnchorComplete = Marshal.PtrToStructure<XrEventDataSpatialAnchorCreateCompleteFB>(XrEventDataBufferData);
					Log.Info($"spatialAnchorComplete.result: {spatialAnchorComplete.result}");
					Anchors.Add(new Anchor
					{
						requestId = spatialAnchorComplete.requestId,
						xrSpace = spatialAnchorComplete.space,
						uuid = spatialAnchorComplete.uuid,
					});

					if (isComponentSupported(spatialAnchorComplete.space, XrSpaceComponentTypeFB.XR_SPACE_COMPONENT_TYPE_STORABLE_FB))
					{
						// When anchor is first created, the component STORABLE is not yet set. So we do it here.
						XrSpaceComponentStatusSetInfoFB setComponentInfo = new XrSpaceComponentStatusSetInfoFB(XrSpaceComponentTypeFB.XR_SPACE_COMPONENT_TYPE_STORABLE_FB, true);
						XrResult result = xrSetSpaceComponentStatusFB(spatialAnchorComplete.space, setComponentInfo, out XrAsyncRequestIdFB requestId);

						if (result == XrResult.XR_ERROR_SPACE_COMPONENT_STATUS_ALREADY_SET_FB)
						{
							Log.Err("XR_ERROR_SPACE_COMPONENT_STATUS_ALREADY_SET_FB");

							// Save space
							saveSpace(spatialAnchorComplete.space);
						}
					}

					break;
				case XrStructureType.XR_TYPE_EVENT_DATA_SPACE_SET_STATUS_COMPLETE_FB:
					XrEventDataSpaceSetStatusCompleteFB setStatusComplete = Marshal.PtrToStructure<XrEventDataSpaceSetStatusCompleteFB>(XrEventDataBufferData);
					if (setStatusComplete.result == XrResult.Success)
					{
						if (setStatusComplete.componentType == XrSpaceComponentTypeFB.XR_SPACE_COMPONENT_TYPE_STORABLE_FB)
						{
							// Save space
							saveSpace(setStatusComplete.space);
						}
						else if (setStatusComplete.componentType == XrSpaceComponentTypeFB.XR_SPACE_COMPONENT_TYPE_LOCATABLE_FB)
						{
							// TODO might need to add to Anchor list from here, if Query event handler calls set component for LOCATABLE
						}
					}
					else
					{
						Log.Err("Error from XR_TYPE_EVENT_DATA_SPACE_SET_STATUS_COMPLETE_FB: " + setStatusComplete.result);
					}

					break;
				default:
					break;
			}
		}

		bool isComponentSupported(XrSpace space, XrSpaceComponentTypeFB type)
		{
			uint numComponents = 0;
			XrResult result = xrEnumerateSpaceSupportedComponentsFB(space, 0, out numComponents, null);
			XrSpaceComponentTypeFB[] componentTypes = new XrSpaceComponentTypeFB[numComponents];
			result = xrEnumerateSpaceSupportedComponentsFB(space, numComponents, out numComponents, componentTypes);

			if (result != XrResult.Success)
			{
				Log.Err($"Error isComponentSupported: {result}");
			}

			for (int i = 0; i < numComponents; i++)
			{
				if (componentTypes[i] == type)
					return true;
			}

			return false;
		}

		void saveSpace(XrSpace space)
		{
			// TODO
		}
	}
}