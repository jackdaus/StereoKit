﻿using System;
using XrFlags64 = System.UInt64;

namespace StereoKitTest.Tools.SpatialEntityFBExt
{
	enum XrSpaceLocationFlags : XrFlags64
	{
		None = 0,
		XR_SPACE_LOCATION_ORIENTATION_VALID_BIT = 0x00000001,
		XR_SPACE_LOCATION_POSITION_VALID_BIT = 0x00000002,
		XR_SPACE_LOCATION_ORIENTATION_TRACKED_BIT = 0x00000004,
		XR_SPACE_LOCATION_POSITION_TRACKED_BIT = 0x00000008
	}

	enum XrStructureType : UInt32
	{
		XR_TYPE_EVENT_DATA_BUFFER = 16,
		XR_TYPE_EVENT_DATA_SESSION_STATE_CHANGED = 18,
		XR_TYPE_SPACE_LOCATION = 42,

		// XR_FB_spatial_entity
		XR_TYPE_SYSTEM_SPATIAL_ENTITY_PROPERTIES_FB = 1000113004,
		XR_TYPE_SPATIAL_ANCHOR_CREATE_INFO_FB = 1000113003,
		XR_TYPE_SPACE_COMPONENT_STATUS_SET_INFO_FB = 1000113007,
		XR_TYPE_SPACE_COMPONENT_STATUS_FB = 1000113001,
		XR_TYPE_EVENT_DATA_SPATIAL_ANCHOR_CREATE_COMPLETE_FB = 1000113005,
		XR_TYPE_EVENT_DATA_SPACE_SET_STATUS_COMPLETE_FB = 1000113006,

		// XR_FB_spatial_entity_storage
		XR_TYPE_SPACE_SAVE_INFO_FB = 1000158000,
		XR_TYPE_SPACE_ERASE_INFO_FB = 1000158001,
		XR_TYPE_EVENT_DATA_SPACE_SAVE_COMPLETE_FB = 1000158106,
		XR_TYPE_EVENT_DATA_SPACE_ERASE_COMPLETE_FB = 1000158107,
	}

	enum XrResult : Int32
	{
		Success = 0,
		XR_ERROR_VALIDATION_FAILURE = -1,

		// XR_FB_spatial_entity
		XR_ERROR_SPACE_COMPONENT_NOT_SUPPORTED_FB = -1000113000,
		XR_ERROR_SPACE_COMPONENT_NOT_ENABLED_FB = -1000113001,
		XR_ERROR_SPACE_COMPONENT_STATUS_PENDING_FB = -1000113002,
		XR_ERROR_SPACE_COMPONENT_STATUS_ALREADY_SET_FB = -1000113003,
	}

	// Provided by XR_FB_spatial_entity
	enum XrSpaceComponentTypeFB : UInt32
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

	// Provided by XR_FB_spatial_entity_storage
	enum XrSpaceStorageLocationFB : UInt32
	{
		XR_SPACE_STORAGE_LOCATION_INVALID_FB = 0,
		XR_SPACE_STORAGE_LOCATION_LOCAL_FB = 1,
		XR_SPACE_STORAGE_LOCATION_MAX_ENUM_FB = 0x7FFFFFFF
	}

	enum XrSpacePersistenceModeFB
	{
		XR_SPACE_PERSISTENCE_MODE_INVALID_FB = 0,
		XR_SPACE_PERSISTENCE_MODE_INDEFINITE_FB = 1,
		XR_SPACE_PERSISTENCE_MODE_MAX_ENUM_FB = 0x7FFFFFFF
	}
}
