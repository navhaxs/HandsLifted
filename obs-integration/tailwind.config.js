/** @type {import('tailwindcss').Config} */
export default {
    content: ['./src/**/*.{html,js,svelte,ts}'],
    theme: {
        extend: {
            padding: {
                '1/2': '50%',
                full: '100%',
            },
        },
    },
    plugins: [],
}