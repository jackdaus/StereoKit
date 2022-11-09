using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;

using XrSpace            = System.UInt64;
using XrTime             = System.Int64;
using XrDuration         = System.Int64;
using XrSession          = System.UInt64;
using XrFlags64          = System.UInt64;
using XrAsyncRequestIdFB = System.UInt64;

namespace StereoKit.Framework
{
	class SpatialEntityFBExt : IStepper
	{
		bool extAvailable;
		bool enabled;
		bool enabledPassthrough;
		bool enableOnInitialize;
		bool passthroughRunning;
		XrPassthroughFB activePassthrough = new XrPassthroughFB();
		XrPassthroughLayerFB activeLayer  = new XrPassthroughLayerFB();

		Color oldColor;
		bool oldSky;

		// XR_FB_spatial_entity
		bool spatialExtAvailable;

		// Constants
		const XrDuration XR_INFINITE_DURATION = 0x7fffffffffffffff;

		public List<Anchor> Anchors = new List<Anchor>();

		public class Anchor
		{
			public Guid uuid;
			public XrSpace xrSpace;
			public Pose pose;
			public XrSession requestId;
		}

		public bool Available => extAvailable;
		public bool SpatialAvailable => spatialExtAvailable;
		public bool Enabled { get => extAvailable && enabled; set => enabled = value; }
		public bool EnabledPassthrough
		{
			get => enabledPassthrough; set
			{
				if (Available && enabledPassthrough != value)
				{
					enabledPassthrough = value;
					if (enabledPassthrough) StartPassthrough();
					if (!enabledPassthrough) EndPassthrough();
				}
			}
		}

		public SpatialEntityFBExt() : this(true) { }
		public SpatialEntityFBExt(bool enabled = true)
		{
			if (SK.IsInitialized)
				Log.Err("PassthroughFBExt must be constructed before StereoKit is initialized!");
			Backend.OpenXR.RequestExt("XR_EXT_uuid");
			Backend.OpenXR.RequestExt("XR_FB_spatial_entity");
			Backend.OpenXR.RequestExt("XR_FB_passthrough");
			enableOnInitialize = enabled;
		}

		public bool Initialize()
		{
			//extAvailable =
			//	Backend.XRType == BackendXRType.OpenXR &&
			//	Backend.OpenXR.ExtEnabled("XR_FB_passthrough") &&firefox
			//	LoadBindings();

			bool isOpenXR = Backend.XRType == BackendXRType.OpenXR;
			bool isExtEnabled = Backend.OpenXR.ExtEnabled("XR_FB_passthrough");
			bool isLoadBindingsSuccessful = LoadBindings();


			bool isSpatialExtEnabled = Backend.OpenXR.ExtEnabled("XR_FB_spatial_entity");
			bool isLoadSpatialBindingsSuccessful = LoadSpatialBindings();
			Log.Info($"LoadSpatialBindings: {(isLoadSpatialBindingsSuccessful ? "SUCCESS" : "FAIL")}");

			extAvailable = isOpenXR && isExtEnabled && isLoadBindingsSuccessful;
			spatialExtAvailable = isOpenXR && isSpatialExtEnabled && isLoadSpatialBindingsSuccessful;

			// Set up xrPollEvent subscriptions
			if (spatialExtAvailable)
			{
				Backend.OpenXR.OnPollEvent += callbackFunc1;
			}

			if (enableOnInitialize)
				EnabledPassthrough = true;
			return true;
		}

		[StructLayout(LayoutKind.Sequential)]
		struct XrEventDataBuffer
		{
			public XrStructureType type;
			public IntPtr next;
			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 4000)]
			public byte[] flags;

			public XrEventDataBuffer()
			{
				type = XrStructureType.XR_TYPE_EVENT_DATA_BUFFER;
				next = IntPtr.Zero;
				flags = new byte[4000];
			}
		}

		struct XrEventDataSessionStateChanged
		{
			public XrStructureType type;
			public IntPtr next;
			public XrSession session;
			public XrSessionState state;
			public XrTime time;
		}

		enum XrSessionState : uint
		{
			XR_SESSION_STATE_UNKNOWN = 0,
			XR_SESSION_STATE_IDLE = 1,
			XR_SESSION_STATE_READY = 2,
			XR_SESSION_STATE_SYNCHRONIZED = 3,
			XR_SESSION_STATE_VISIBLE = 4,
			XR_SESSION_STATE_FOCUSED = 5,
			XR_SESSION_STATE_STOPPING = 6,
			XR_SESSION_STATE_LOSS_PENDING = 7,
			XR_SESSION_STATE_EXITING = 8,
			XR_SESSION_STATE_MAX_ENUM = 0x7FFFFFFF
		}


		void callbackFunc1(IntPtr XrEventDataBufferData)
		{
			XrEventDataBuffer myBuffer = Marshal.PtrToStructure<XrEventDataBuffer>(XrEventDataBufferData);
			Log.Info("XrEventDataBuffer Type: " + myBuffer.type.ToString());

			if (myBuffer.type == XrStructureType.XR_TYPE_EVENT_DATA_SESSION_STATE_CHANGED)
			{
				XrEventDataSessionStateChanged myStateChangedData = Marshal.PtrToStructure<XrEventDataSessionStateChanged>(XrEventDataBufferData);
				Log.Info("myStateChangedData Type: " + myStateChangedData.type.ToString());

			}

			if (myBuffer.type == XrStructureType.XR_TYPE_EVENT_DATA_SPATIAL_ANCHOR_CREATE_COMPLETE_FB)
			{
				XrEventDataSpatialAnchorCreateCompleteFB spatialAnchorComplete = Marshal.PtrToStructure<XrEventDataSpatialAnchorCreateCompleteFB>(XrEventDataBufferData);
				Log.Info($"spatialAnchorComplete.result: {spatialAnchorComplete.result}");
				Anchors.Add(new Anchor
				{
					requestId = spatialAnchorComplete.requestId,
					xrSpace = spatialAnchorComplete.space,
					uuid = spatialAnchorComplete.uuid,
				});
			}
		}

		public void Step()
		{
			if (!EnabledPassthrough) return;

			XrCompositionLayerPassthroughFB layer = new XrCompositionLayerPassthroughFB(
				XrCompositionLayerFlags.BLEND_TEXTURE_SOURCE_ALPHA_BIT, activeLayer);
			Backend.OpenXR.AddCompositionLayer(layer, -1);

			Anchors.ForEach(a =>
			{
				XrSpaceLocation spaceLocation = new XrSpaceLocation { type = XrStructureType.XR_TYPE_SPACE_LOCATION };

				// TODO consider using XrFrameState.predictedDisplayTime for time
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
			EnabledPassthrough = false;
		}

		void StartPassthrough()
		{
			if (!extAvailable) return;
			if (passthroughRunning) return;
			passthroughRunning = true;

			oldColor = Renderer.ClearColor;
			oldSky = Renderer.EnableSky;

			XrResult result = xrCreatePassthroughFB(
				Backend.OpenXR.Session,
				new XrPassthroughCreateInfoFB(XrPassthroughFlagsFB.IS_RUNNING_AT_CREATION_BIT_FB),
				out activePassthrough);

			result = xrCreatePassthroughLayerFB(
				Backend.OpenXR.Session,
				new XrPassthroughLayerCreateInfoFB(activePassthrough, XrPassthroughFlagsFB.IS_RUNNING_AT_CREATION_BIT_FB, XrPassthroughLayerPurposeFB.RECONSTRUCTION_FB),
				out activeLayer);

			Renderer.ClearColor = Color.BlackTransparent;
			Renderer.EnableSky = false;
		}

		void EndPassthrough()
		{
			if (!passthroughRunning) return;
			passthroughRunning = false;

			xrPassthroughPauseFB(activePassthrough);
			xrDestroyPassthroughLayerFB(activeLayer);
			xrDestroyPassthroughFB(activePassthrough);

			Renderer.ClearColor = oldColor;
			Renderer.EnableSky = oldSky;
		}

		// XR_FB_spatial_entity
		public bool CreateAnchor(Pose pose)
		{
			Log.Info("Begin CreateAnchor");

			var anchorCreateInfo = new XrSpatialAnchorCreateInfoFB(Backend.OpenXR.Space, pose, Backend.OpenXR.Time);

			XrResult result = xrCreateSpatialAnchorFB(
				Backend.OpenXR.Session,
				anchorCreateInfo,
				out XrAsyncRequestIdFB requestId);

			Log.Info($"xrCreateSpatialAnchorFB initiated. The request id is: {requestId}. Result: {result}");

			return result == XrResult.Success;
		}


		#region OpenXR native bindings and types
		enum XrStructureType : uint // Must be UInt32!!
		{
			XR_TYPE_EVENT_DATA_BUFFER = 16,

			XR_TYPE_EVENT_DATA_SESSION_STATE_CHANGED = 18, // just for testing TODO remove?

			XR_TYPE_SPACE_LOCATION = 42,

			XR_TYPE_PASSTHROUGH_CREATE_INFO_FB = 1000118001,
			XR_TYPE_PASSTHROUGH_LAYER_CREATE_INFO_FB = 1000118002,
			XR_TYPE_PASSTHROUGH_STYLE_FB = 1000118020,
			XR_TYPE_COMPOSITION_LAYER_PASSTHROUGH_FB = 1000118003,

			// XR_FB_spatial_entity New Enum Constants
			XR_TYPE_SYSTEM_SPATIAL_ENTITY_PROPERTIES_FB = 1000113004,
			XR_TYPE_SPATIAL_ANCHOR_CREATE_INFO_FB = 1000113003,
			XR_TYPE_SPACE_COMPONENT_STATUS_SET_INFO_FB = 1000113007,
			XR_TYPE_SPACE_COMPONENT_STATUS_FB = 1000113001,
			XR_TYPE_EVENT_DATA_SPATIAL_ANCHOR_CREATE_COMPLETE_FB = 1000113005,
			XR_TYPE_EVENT_DATA_SPACE_SET_STATUS_COMPLETE_FB = 1000113006,
		}
		enum XrPassthroughFlagsFB : XrSession
		{
			None = 0,
			IS_RUNNING_AT_CREATION_BIT_FB = 0x00000001
		}
		enum XrCompositionLayerFlags : XrSession
		{
			None = 0,
			CORRECT_CHROMATIC_ABERRATION_BIT = 0x00000001,
			BLEND_TEXTURE_SOURCE_ALPHA_BIT = 0x00000002,
			UNPREMULTIPLIED_ALPHA_BIT = 0x00000004,
		}
		enum XrPassthroughLayerPurposeFB : uint
		{
			RECONSTRUCTION_FB = 0,
			PROJECTED_FB = 1,
			TRACKED_KEYBOARD_HANDS_FB = 1000203001,
			MAX_ENUM_FB = 0x7FFFFFFF,
		}
		enum XrResult : int
		{
			Success = 0,
			XR_ERROR_VALIDATION_FAILURE = -1,
			// Provided by XR_FB_spatial_entity
			XR_ERROR_SPACE_COMPONENT_NOT_SUPPORTED_FB = -1000113000,
			XR_ERROR_SPACE_COMPONENT_NOT_ENABLED_FB = -1000113001,
			XR_ERROR_SPACE_COMPONENT_STATUS_PENDING_FB = -1000113002,
			XR_ERROR_SPACE_COMPONENT_STATUS_ALREADY_SET_FB = -1000113003,
		}

		// XR_FB_spatial_entity New Enums
		enum XrSpaceComponentTypeFB : uint
		{
			XR_SPACE_COMPONENT_TYPE_LOCATABLE_FB = 0,
			XR_SPACE_COMPONENT_TYPE_STORABLE_FB = 1,
			XR_SPACE_COMPONENT_TYPE_BOUNDED_2D_FB = 3,
			XR_SPACE_COMPONENT_TYPE_BOUNDED_3D_FB = 4,
			XR_SPACE_COMPONENT_TYPE_SEMANTIC_LABELS_FB = 5,
			XR_SPACE_COMPONENT_TYPE_ROOM_LAYOUT_FB = 6,
			XR_SPACE_COMPONENT_TYPE_SPACE_CONTAINER_FB = 7,
			XR_SPACE_COMPONENT_TYPE_MAX_ENUM_FB = 0x7FFFFFFF
		}


		// Other enums
		[Flags]
		enum XrSpaceLocationFlags : XrFlags64
		{
			None = 0,
			XR_SPACE_LOCATION_ORIENTATION_VALID_BIT = 0x00000001,
			XR_SPACE_LOCATION_POSITION_VALID_BIT = 0x00000002,
			XR_SPACE_LOCATION_ORIENTATION_TRACKED_BIT = 0x00000004,
			XR_SPACE_LOCATION_POSITION_TRACKED_BIT = 0x00000008
		}

#pragma warning disable 0169 // handle is not "used", but required for interop
		struct XrPassthroughFB { ulong handle; }
		struct XrPassthroughLayerFB { ulong handle; }

		// SPATIAL_EXT New Base Types
		/// <summary>
		/// Represents a request to the spatial entity system. Several functions in this and other 
		/// extensions will populate an output variable of this type so that an application can 
		/// use it when referring to a specific request.
		/// </summary>
		//struct XrAsyncRequestIdFB { public ulong handle; }
#pragma warning restore 0169

		[StructLayout(LayoutKind.Sequential)]
		struct XrPassthroughCreateInfoFB
		{
			private XrStructureType type;
			public IntPtr next;
			public XrPassthroughFlagsFB flags;

			public XrPassthroughCreateInfoFB(XrPassthroughFlagsFB passthroughFlags)
			{
				type = XrStructureType.XR_TYPE_PASSTHROUGH_CREATE_INFO_FB;
				next = IntPtr.Zero;
				flags = passthroughFlags;
			}
		}
		[StructLayout(LayoutKind.Sequential)]
		struct XrPassthroughLayerCreateInfoFB
		{
			private XrStructureType type;
			public IntPtr next;
			public XrPassthroughFB passthrough;
			public XrPassthroughFlagsFB flags;
			public XrPassthroughLayerPurposeFB purpose;

			public XrPassthroughLayerCreateInfoFB(XrPassthroughFB passthrough, XrPassthroughFlagsFB flags, XrPassthroughLayerPurposeFB purpose)
			{
				type = XrStructureType.XR_TYPE_PASSTHROUGH_LAYER_CREATE_INFO_FB;
				next = IntPtr.Zero;
				this.passthrough = passthrough;
				this.flags = flags;
				this.purpose = purpose;
			}
		}
		[StructLayout(LayoutKind.Sequential)]
		struct XrPassthroughStyleFB
		{
			public XrStructureType type;
			public IntPtr next;
			public float textureOpacityFactor;
			public Color edgeColor;
			public XrPassthroughStyleFB(float textureOpacityFactor, Color edgeColor)
			{
				type = XrStructureType.XR_TYPE_PASSTHROUGH_STYLE_FB;
				next = IntPtr.Zero;
				this.textureOpacityFactor = textureOpacityFactor;
				this.edgeColor = edgeColor;
			}
		}
		[StructLayout(LayoutKind.Sequential)]
		struct XrCompositionLayerPassthroughFB
		{
			public XrStructureType type;
			public IntPtr next;
			public XrCompositionLayerFlags flags;
			public ulong space;
			public XrPassthroughLayerFB layerHandle;
			public XrCompositionLayerPassthroughFB(XrCompositionLayerFlags flags, XrPassthroughLayerFB layerHandle)
			{
				type = XrStructureType.XR_TYPE_COMPOSITION_LAYER_PASSTHROUGH_FB;
				next = IntPtr.Zero;
				space = 0;
				this.flags = flags;
				this.layerHandle = layerHandle;
			}
		}



		// XR_EXT_uuid
		[StructLayout(LayoutKind.Sequential)]
		struct XrUuidEXT
		{
			// TODO not sure if this is correct
			public byte[] data;

			public XrUuidEXT()
			{
				data = new byte[16];
			}
		}


		// XR_FB_spatial_entity New Structures
		[StructLayout(LayoutKind.Sequential)]
		struct XrSystemSpatialEntityPropertiesFB
		{
			private XrStructureType type;
			public IntPtr next;

			/// <summary>
			/// a boolean value that determines if spatial entities are supported by the system.
			/// </summary>
			public bool supportsSpatialEntity;

			public XrSystemSpatialEntityPropertiesFB(bool supportsSpatialEntity)
			{
				type = XrStructureType.XR_TYPE_SYSTEM_SPATIAL_ENTITY_PROPERTIES_FB;
				next = IntPtr.Zero;
				this.supportsSpatialEntity = supportsSpatialEntity;
			}
		}

		/// <summary>
		/// Parameters to create a new spatial anchor
		/// </summary>
		[StructLayout(LayoutKind.Sequential)]
		struct XrSpatialAnchorCreateInfoFB
		{
			private XrStructureType type;
			public IntPtr next;
			public XrSpace space;
			public XrPosef poseInSpace;
			public XrTime time;

			public XrSpatialAnchorCreateInfoFB(XrSpace space, XrPosef poseInSpace, XrTime time)
			{
				type = XrStructureType.XR_TYPE_SPATIAL_ANCHOR_CREATE_INFO_FB;
				next = IntPtr.Zero;
				this.space = space;
				this.poseInSpace = poseInSpace;
				this.time = time;
			}
		}

		/// <summary>
		/// Enables or disables the specified component for the specified spatial entity
		/// </summary>
		[StructLayout(LayoutKind.Sequential)]
		struct XrSpaceComponentStatusSetInfoFB
		{
			private XrStructureType type;

			public IntPtr next;

			/// <summary>
			/// the component whose status is to be set
			/// </summary>
			public XrSpaceComponentTypeFB componentType;

			public bool enabled;

			/// <summary>
			/// the number of nanoseconds before the operation should be cancelled. A value of XR_INFINITE_DURATION 
			/// indicates to never time out.
			/// </summary>
			public XrDuration timeout;

			public XrSpaceComponentStatusSetInfoFB(XrSpaceComponentTypeFB componentType, bool enabled)
			{
				type = XrStructureType.XR_TYPE_SPACE_COMPONENT_STATUS_SET_INFO_FB;
				next = IntPtr.Zero;
				this.componentType = componentType;
				this.enabled = enabled;
				timeout = XR_INFINITE_DURATION;
			}
		}

		/// <summary>
		/// Holds information on the current state of a component.
		/// </summary>
		[StructLayout(LayoutKind.Sequential)]
		struct XrSpaceComponentStatusFB
		{
			XrStructureType type;
			public IntPtr next;

			/// <summary>
			/// a boolean value that determines if a component is currently enabled or disabled.
			/// </summary>
			public bool enabled;

			/// <summary>
			/// a boolean value that determines if the component’s enabled state is about to change.
			/// </summary>
			public bool changePending;

			public XrSpaceComponentStatusFB()
			{
				type = XrStructureType.XR_TYPE_SPACE_COMPONENT_STATUS_FB;
				next = IntPtr.Zero;
				enabled = false;
				changePending = false;
			}
		}

		/// <summary>
		/// It describes the result of a request to create a new spatial anchor. Once this event is posted,
		/// it is the application's responsibility to take ownership of the XrSpace. The XrSession passed 
		/// into xrCreateSpatialAnchorFB is the parent handle of the newly created XrSpace.
		/// </summary>
		[StructLayout(LayoutKind.Sequential)]
		struct XrEventDataSpatialAnchorCreateCompleteFB
		{
			public XrStructureType type;
			public IntPtr next;
			public XrAsyncRequestIdFB requestId;
			public XrResult result;
			public XrSpace space;
			public Guid uuid; // NOTE: not sure if this data is  Marshalled correctly... need to test
							  //public XrUuidEXT uuid;

			public XrEventDataSpatialAnchorCreateCompleteFB()
			{
				type = XrStructureType.XR_TYPE_EVENT_DATA_SPATIAL_ANCHOR_CREATE_COMPLETE_FB;
				next = IntPtr.Zero;
				requestId = 0;
				result = 0;
				space = 0;
				uuid = Guid.Empty;
				//uuid = new XrUuidEXT();
			}
		}

		/// <summary>
		/// Describes the result of a request to enable or disable a component of a spatial entity.
		/// </summary>
		[StructLayout(LayoutKind.Sequential)]
		struct XrEventDataSpaceSetStatusCompleteFB
		{
			private XrStructureType type;
			public IntPtr next;
			public XrAsyncRequestIdFB requestId;
			public XrResult result;
			public XrSpace space;
			public XrUuidEXT uuid;
			public XrSpaceComponentTypeFB componentType;

			/// <summary>
			/// a boolean value indicating whether the component is now enabled or disabled.
			/// </summary>
			public bool enabled;

			public XrEventDataSpaceSetStatusCompleteFB()
			{
				type = XrStructureType.XR_TYPE_EVENT_DATA_SPACE_SET_STATUS_COMPLETE_FB;
				next = IntPtr.Zero;
				requestId = 0;
				result = 0;
				space = 0;
				uuid = new XrUuidEXT();
				componentType = 0;
				enabled = false;
			}
		}



		//////////////// Misc Structs
		///

		[StructLayout(LayoutKind.Sequential)]
		struct XrSpaceLocation
		{
			public XrStructureType type;
			public IntPtr next;
			public XrSpaceLocationFlags locationFlags;
			public XrPosef pose;
		}

		[StructLayout(LayoutKind.Sequential)]
		struct XrPosef
		{
			public XrQuaternionf orientation;
			public XrVector3f position;

			public static implicit operator Pose(XrPosef p) => new Pose
			{
				position    = new Vec3(p.position.x, p.position.y, p.position.z),
				orientation = new Quat(p.orientation.x, p.orientation.y, p.orientation.z, p.orientation.w)
			};

			public static implicit operator XrPosef(Pose p) => new XrPosef
			{
				orientation = new XrQuaternionf(p.orientation.x, p.orientation.y, p.orientation.z, p.orientation.w),
				position    = new XrVector3f(p.position.x, p.position.y, p.position.z),
			};
		}

		[StructLayout(LayoutKind.Sequential)]
		struct XrQuaternionf
		{
			public float x;
			public float y;
			public float z;
			public float w;

			public XrQuaternionf()
			{
				x = 0; y = 0; z = 0; w = 0;
			}

			public XrQuaternionf(float x, float y, float z, float w)
			{
				this.x = x;
				this.y = y;
				this.z = z;
				this.w = w;
			}
		}

		[StructLayout(LayoutKind.Sequential)]
		struct XrVector3f
		{
			public float x;
			public float y;
			public float z;

			public XrVector3f()
			{
				x = 0; y = 0; z = 0;
			}

			public XrVector3f(float x, float y, float z)
			{
				this.x = x;
				this.y = y;
				this.z = z;
			}
		}




		delegate XrResult del_xrCreatePassthroughFB(ulong session, [In] XrPassthroughCreateInfoFB createInfo, out XrPassthroughFB outPassthrough);
		delegate XrResult del_xrDestroyPassthroughFB(XrPassthroughFB passthrough);
		delegate XrResult del_xrPassthroughStartFB(XrPassthroughFB passthrough);
		delegate XrResult del_xrPassthroughPauseFB(XrPassthroughFB passthrough);
		delegate XrResult del_xrCreatePassthroughLayerFB(ulong session, [In] XrPassthroughLayerCreateInfoFB createInfo, out XrPassthroughLayerFB outLayer);
		delegate XrResult del_xrDestroyPassthroughLayerFB(XrPassthroughLayerFB layer);
		delegate XrResult del_xrPassthroughLayerPauseFB(XrPassthroughLayerFB layer);
		delegate XrResult del_xrPassthroughLayerResumeFB(XrPassthroughLayerFB layer);
		delegate XrResult del_xrPassthroughLayerSetStyleFB(XrPassthroughLayerFB layer, [In] XrPassthroughStyleFB style);


		////////// XR_FB_spatial_entity New Functions
		///

		/// <summary>
		/// Creates a Spatial Anchor using the specified tracking origin and pose relative to the specified tracking origin.
		/// </summary>
		/// <param name="session"></param>
		/// <param name="info"></param>
		/// <param name="requestId"></param>
		/// <returns></returns>
		delegate XrResult del_xrCreateSpatialAnchorFB(
			ulong session,
			[In] XrSpatialAnchorCreateInfoFB info,
			out XrAsyncRequestIdFB requestId);

		/// <summary>
		/// Gets the UUID for a spatial entity.
		/// </summary>
		/// <param name="space">The XrSpace handle of a spatial entity.</param>
		/// <param name="uuid">An output parameter pointing to the entity’s UUID.</param>
		/// <returns></returns>
		delegate XrResult del_xrGetSpaceUuidFB(
			XrSpace space,
			out XrUuidEXT uuid);

		/// <summary>
		/// Lists any component types that an entity supports.
		/// </summary>
		/// <param name="space">the XrSpace handle to the spatial entity.</param>
		/// <param name="componentTypeCapacityInput">the capacity of the componentTypes array, or 0 to indicate a request to retrieve the required capacity.</param>
		/// <param name="componentTypeCountOutput">a pointer to the count of componentTypes written, or a pointer to the required capacity in the case that componentTypeCapacityInput is insufficient.</param>
		/// <param name="componentTypes">a pointer to an array of XrSpaceComponentTypeFB values, but can be NULL if componentTypeCapacityInput is 0.</param>
		/// <returns></returns>
		delegate XrResult del_xrEnumerateSpaceSupportedComponentsFB(
			XrSpace space,
			uint componentTypeCapacityInput,
			out uint componentTypeCountOutput,
			XrSpaceComponentTypeFB[] componentTypes);           // TODO not sure if this is correct...


		/// <summary>
		/// Enables or disables the specified component for the specified entity.
		/// </summary>
		/// <param name="space">the XrSpace handle to the spatial entity.</param>
		/// <param name="info">a pointer to an XrSpaceComponentStatusSetInfoFB structure containing information about the component to be enabled or disabled.</param>
		/// <param name="requestId">the output parameter that points to the ID of this asynchronous request.</param>
		/// <returns></returns>
		delegate XrResult del_xrSetSpaceComponentStatusFB(
			XrSpace space,
			[In] XrSpaceComponentStatusSetInfoFB info,
			out XrAsyncRequestIdFB requestId);

		/// <summary>
		/// Gets the current status of the specified component for the specified entity.
		/// </summary>
		/// <param name="space">the XrSpace handle of a spatial entity.</param>
		/// <param name="componentType">the component type to query.</param>
		/// <param name="status">an output parameter pointing to the structure containing the status of the component that was queried.</param>
		/// <returns></returns>
		delegate XrResult del_xrGetSpaceComponentStatusFB(
			XrSpace space,
			XrSpaceComponentTypeFB componentType,
			out XrSpaceComponentStatusFB status);


		////////// Additional Misc New Functions
		///

		delegate XrResult del_xrLocateSpace(
			XrSpace space,
			XrSpace baseSpace,
			XrTime time,
			out XrSpaceLocation location);


		del_xrCreatePassthroughFB xrCreatePassthroughFB;
		del_xrDestroyPassthroughFB xrDestroyPassthroughFB;
		del_xrPassthroughStartFB xrPassthroughStartFB;
		del_xrPassthroughPauseFB xrPassthroughPauseFB;
		del_xrCreatePassthroughLayerFB xrCreatePassthroughLayerFB;
		del_xrDestroyPassthroughLayerFB xrDestroyPassthroughLayerFB;
		del_xrPassthroughLayerPauseFB xrPassthroughLayerPauseFB;
		del_xrPassthroughLayerResumeFB xrPassthroughLayerResumeFB;
		del_xrPassthroughLayerSetStyleFB xrPassthroughLayerSetStyleFB;

		// XR_FB_spatial_entity New Functions
		del_xrCreateSpatialAnchorFB xrCreateSpatialAnchorFB;
		del_xrGetSpaceUuidFB xrGetSpaceUuidFB;
		del_xrEnumerateSpaceSupportedComponentsFB xrEnumerateSpaceSupportedComponentsFB;
		del_xrSetSpaceComponentStatusFB xrSetSpaceComponentStatusFB;
		del_xrGetSpaceComponentStatusFB xrGetSpaceComponentStatusFB;

		// Misc New Functions
		del_xrLocateSpace xrLocateSpace;

		bool LoadBindings()
		{
			xrCreatePassthroughFB = Backend.OpenXR.GetFunction<del_xrCreatePassthroughFB>("xrCreatePassthroughFB");
			xrDestroyPassthroughFB = Backend.OpenXR.GetFunction<del_xrDestroyPassthroughFB>("xrDestroyPassthroughFB");
			xrPassthroughStartFB = Backend.OpenXR.GetFunction<del_xrPassthroughStartFB>("xrPassthroughStartFB");
			xrPassthroughPauseFB = Backend.OpenXR.GetFunction<del_xrPassthroughPauseFB>("xrPassthroughPauseFB");
			xrCreatePassthroughLayerFB = Backend.OpenXR.GetFunction<del_xrCreatePassthroughLayerFB>("xrCreatePassthroughLayerFB");
			xrDestroyPassthroughLayerFB = Backend.OpenXR.GetFunction<del_xrDestroyPassthroughLayerFB>("xrDestroyPassthroughLayerFB");
			xrPassthroughLayerPauseFB = Backend.OpenXR.GetFunction<del_xrPassthroughLayerPauseFB>("xrPassthroughLayerPauseFB");
			xrPassthroughLayerResumeFB = Backend.OpenXR.GetFunction<del_xrPassthroughLayerResumeFB>("xrPassthroughLayerResumeFB");
			xrPassthroughLayerSetStyleFB = Backend.OpenXR.GetFunction<del_xrPassthroughLayerSetStyleFB>("xrPassthroughLayerSetStyleFB");

			return
				xrCreatePassthroughFB != null &&
				xrDestroyPassthroughFB != null &&
				xrPassthroughStartFB != null &&
				xrPassthroughPauseFB != null &&
				xrCreatePassthroughLayerFB != null &&
				xrDestroyPassthroughLayerFB != null &&
				xrPassthroughLayerPauseFB != null &&
				xrPassthroughLayerResumeFB != null &&
				xrPassthroughLayerSetStyleFB != null;
		}

		// XR_FB_spatial_entity
		bool LoadSpatialBindings()
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
		#endregion

	}
}