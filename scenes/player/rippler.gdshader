shader_type canvas_item;

uniform vec4 ripple_color : source_color = vec4(0.3, 0.0, 1.0, 1.0);
uniform float circle_radius : hint_range(0.0, 2.0) = 0.25; // where the ripple begins
uniform float speed : hint_range(0.1, 5.0) = 0.5;        // slower expansion
uniform float thickness : hint_range(0.001, 0.2) = 0.5; // sharpness of rings
uniform float spacing : hint_range(0.1, 1.0) = 0.1;      // distance between ripple rings
uniform float fade : hint_range(0.1, 5.0) = 1.0;         // how quickly ripples fade
uniform float intensity : hint_range(0.5, 5.0) = 2.0;    // brightness boost

void fragment() {
    // Normalize UV [-1..1]
    vec2 uv = (UV - 0.5) * 2.0;
    float dist = length(uv);

    // Distance measured from the circle’s edge
    float edge_dist = dist - circle_radius;

    // Skip inside region by making it fully transparent
    if (edge_dist < 0.0) {
        COLOR = vec4(0.0, 0.0, 0.0, 0.0);
    } else {
        // Animate ripple position
        float moving_dist = edge_dist - TIME * speed;

        // Generate repeating ripples
        float ripple = fract(moving_dist / spacing);

        // Thin ring
        float ring = smoothstep(thickness, 0.0, min(ripple, 1.0 - ripple));

        // Fade outward
        float fade_mask = exp(-edge_dist * fade);

        // Final boosted color
        vec4 col = ripple_color * ring * fade_mask * intensity;

        COLOR = vec4(col.rgb, col.a);
    }
}
