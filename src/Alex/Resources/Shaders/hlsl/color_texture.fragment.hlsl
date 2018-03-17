#include "ShaderConstants.fxh"

float4 glintBlend(float4 dest, float4 source) {
	return float4(source.rgb * source.rgb, 0.0) + dest;
}

struct PS_Input
{
	float4 position : SV_Position;
	float4 color : COLOR;
	float2 uv : TEXCOORD_0;

#ifdef ENABLE_FOG
	float4 fogColor : FOG_COLOR;
#endif

#ifdef GLINT
	float2 layer1UV : UV_1;
	float2 layer2UV : UV_2;
#endif
};

struct PS_Output
{
	float4 color : SV_Target;
};

void main( in PS_Input PSInput, out PS_Output PSOutput )
{
#ifdef EFFECTS_OFFSET
	float4 diffuse = TEXTURE_0.Sample(TextureSampler0, PSInput.uv + EFFECT_UV_OFFSET);
#else
	float4 diffuse = TEXTURE_0.Sample(TextureSampler0, PSInput.uv);
#endif

#ifdef ALPHA_TEST

#ifdef ENABLE_VERTEX_TINT_MASK
	if( diffuse.a <= 0.0f )
#else
	if (diffuse.a <= 0.5f)
#endif
	{
		discard;
	}
#endif

#ifdef ENABLE_VERTEX_TINT_MASK
	diffuse.rgb = lerp(diffuse.rgb, diffuse.rgb*PSInput.color.rgb, diffuse.a);
	if (PSInput.color.a > 0.0f) {
		diffuse.a = diffuse.a > 0.0f ? 1.0f : 0.0f; // This line is needed for horse armour icon and dyed leather to work properly
	}
#endif

#ifdef GLINT
	float4 layer1 = TEXTURE_1.Sample(TextureSampler1, frac(PSInput.layer1UV)).rgbr * GLINT_COLOR;
	float4 layer2 = TEXTURE_1.Sample(TextureSampler1, frac(PSInput.layer2UV)).rgbr * GLINT_COLOR;
	float4 glint = (layer1 + layer2);
	glint.rgb *= PSInput.color.a;

	#ifdef INVENTORY
		diffuse.rgb = glint.rgb;
	#else
		diffuse.rgb = glintBlend(diffuse, glint).rgb;
	#endif
#endif

#ifdef USE_OVERLAY
		//use either the diffuse or the OVERLAY_COLOR
	diffuse.rgb = lerp( diffuse, OVERLAY_COLOR, OVERLAY_COLOR.a ).rgb;
#endif

#ifdef ENABLE_FOG
	diffuse.rgb = lerp(diffuse.rgb, PSInput.fogColor.rgb, PSInput.fogColor.a);
#endif

#ifdef ENABLE_VERTEX_TINT_MASK

#ifdef ENABLE_CURRENT_ALPHA_MULTIPLY
	PSOutput.color = diffuse * float4(1.0, 1.0, 1.0, HUD_OPACITY);
#else
	PSOutput.color = diffuse;
#endif

#else
	PSOutput.color = diffuse * PSInput.color;
#endif

#ifdef VR_MODE
	// On Rift, the transition from 0 brightness to the lowest 8 bit value is abrupt, so clamp to 
	// the lowest 8 bit value.
	PSOutput.color = max(PSOutput.color, 1 / 255.0f);
#endif
}