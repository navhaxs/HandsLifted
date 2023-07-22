<script>
	import { onMount } from 'svelte';

	// let photos = [];
	/**
	 * @type {{ Text: string; }}
	 */
	let publishedSlide;

	onMount(async () => {
		// const res = await fetch(`/tutorial/api/album`);
		// photos = await res.json();
		console.log('ello')
		const exampleSocket = new WebSocket('ws://localhost:8979/control/');
		exampleSocket.onmessage = (event) => {
			console.log('event', event.data)
			const parsed = JSON.parse(event.data);
			if (parsed.NewPublish) {
				publishedSlide = parsed.NewPublish.CurrentSlide;
				console.log('recvd', publishedSlide)
			}
		};
	});

	const lyrics = [
		'Oh, happy day, happy day\nYou washed my sin away',
		'Oh, happy day, happy day\nI’ll never be the same',
		'Forever I am changed',
		'When I stand, in that place\nFree at last, meeting face to face',
		'I am Yours, Jesus You are mine',
		'',
		'Endless joy, perfect peace\nEarthly pain finally will cease',
		'Celebrate, Jesus is alive\nHe’s alive'
	];

	let currentText = '';

	// var i = 0;
	// setInterval(() => {
	// 	currentText = lyrics[++i % lyrics.length];
	// }, 2000)
</script>

<div>
	<h1 class="whitespace-pre-wrap">{publishedSlide?.Text}</h1>
</div>

<style lang="scss">
	@import url('https://fonts.googleapis.com/css2?family=Montserrat:wght@600&display=swap');
	div {
		font-family: 'Montserrat', sans-serif;
		position: absolute;
		bottom: 0;
		left: 0;
		right: 0;
		color: white;

		background: rgb(26, 121, 164);
		background: linear-gradient(
			90deg,
			rgba(26, 121, 164, 0.8) 0%,
			rgba(35, 163, 220, 0.8) 50%,
			rgba(93, 181, 218, 0.8) 100%
		);

		height: 21vh;
		text-align: center;

		display: flex;
		justify-content: center;
		vertical-align: baseline;

		flex-direction: column;

		padding: 20px 0;

		h1 {
			font-size: 2.4vw;
			vertical-align: middle;
			margin: 0 auto;
		}

		h1:not(:last-child) {
			margin-bottom: 0.4em;
		}
	}
</style>
