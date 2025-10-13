shader_type canvas_item;

uniform vec4 glow_color = vec4(1.0, 0.843, 0.0, 1.0); // gold
uniform float glow_strength = 10; // stronger glow
uniform float glow_size = 19.0; // pixels

void fragment() {
    vec4 tex_color = texture(TEXTURE, UV);

    // Check alpha of current pixel
    float alpha = tex_color.a;

    // Glow calculation by sampling nearby pixels
    float glow = 0.0;
    for (int x = -int(glow_size); x <= int(glow_size); x++) {
        for (int y = -int(glow_size); y <= int(glow_size); y++) {
            vec2 offset = vec2(x, y) / TEXTURE_PIXEL_SIZE;
            glow += texture(TEXTURE, UV + offset).a;
        }
    }

    glow = glow / pow((glow_size*2+1.0), 2.0); // normalize
    glow = pow(glow, 0.5) * glow_strength;

    // Combine original color with glow
    COLOR = tex_color + vec4(glow_color.rgb, 1.0) * glow * (1.0 - alpha);
}
